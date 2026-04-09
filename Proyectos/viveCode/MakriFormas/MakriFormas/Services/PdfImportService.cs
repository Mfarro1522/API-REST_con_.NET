using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using MakriFormas.Models;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MakriFormas.Services
{
    /// <summary>
    /// Pipeline de importación de archivo:
    ///   1. Extrae texto (multimodal cloud con fallback local)
    ///   2. Estructura el texto con IA cloud
    ///   3. Deduplica contra la base de datos
    /// </summary>
    public static class PdfImportService
    {
        // ── Paso 1: Extracción de texto ───────────────────────────────────────

        public static async Task<string> ExtractTextAsync(string filePath, CancellationToken ct = default)
        {
            if (IsImagePath(filePath))
            {
                return await ExtractFromImageAsync(filePath, ct);
            }

            if (!IsPdfPath(filePath))
            {
                return "[Formato no compatible. Usa un archivo PDF o imagen (PNG/JPG/BMP/TIF).]";
            }

            return await ExtractFromPdfAsync(filePath, ct);
        }

        private static async Task<string> ExtractFromImageAsync(string imagePath, CancellationToken ct)
        {
            var multimodalText = await ExtractWithMultimodalImageAsync(imagePath, ct);
            if (IsLikelyUsefulText(multimodalText))
                return multimodalText;

            var windowsText = await ExtractImageWithWindowsOcrAsync(imagePath, ct);
            if (IsLikelyUsefulText(windowsText))
                return windowsText;

            return "[No se pudo extraer texto suficiente de la imagen. Verifica nitidez y contraste.]";
        }

        private static async Task<string> ExtractFromPdfAsync(string pdfPath, CancellationToken ct)
        {
            var nativeText = ExtractWithIText(pdfPath);
            if (IsLikelyUsefulText(nativeText, threshold: 50))
                return nativeText;

            var multimodalText = await ExtractWithMultimodalPdfAsync(pdfPath, ct);
            if (IsLikelyUsefulText(multimodalText))
                return multimodalText;

            var windowsText = await ExtractWithWindowsOcrAsync(pdfPath, ct);
            if (IsLikelyUsefulText(windowsText))
                return windowsText;

            return "[No se pudo extraer texto suficiente del archivo. Prueba con un PDF o imagen de mayor calidad.]";
        }

        private static bool IsPdfPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImagePath(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveImageMimeType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                _ => "image/png"
            };
        }

        private static string ExtractWithIText(string pdfPath)
        {
            try
            {
                var sb = new StringBuilder();
                using var reader = new PdfReader(pdfPath);
                using var doc = new iText.Kernel.Pdf.PdfDocument(reader);

                for (int i = 1; i <= doc.GetNumberOfPages(); i++)
                {
                    var page = doc.GetPage(i);
                    var text = PdfTextExtractor.GetTextFromPage(page);
                    sb.AppendLine(text);
                }

                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<string> ExtractWithMultimodalImageAsync(string imagePath, CancellationToken ct)
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
                if (imageBytes.Length == 0)
                    return string.Empty;

                var route = await AiProviderRouter.Instance.ExtractTextFromImageAsync(
                    imageBytes,
                    ResolveImageMimeType(imagePath),
                    ct);

                return route.Success ? route.Content.Trim() : string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<string> ExtractWithMultimodalPdfAsync(string pdfPath, CancellationToken ct)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(pdfPath);
                var pdf = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
                if (pdf.PageCount == 0)
                    return string.Empty;

                var sb = new StringBuilder();

                for (uint i = 0; i < pdf.PageCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    using var page = pdf.GetPage(i);
                    var pageImage = await RenderPageAsPngAsync(page);
                    if (pageImage.Length == 0)
                        continue;

                    var route = await AiProviderRouter.Instance.ExtractTextFromImageAsync(pageImage, "image/png", ct);
                    if (route.Success && IsLikelyUsefulText(route.Content))
                    {
                        sb.AppendLine(route.Content.Trim());
                    }
                }

                return sb.ToString().Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<string> ExtractWithWindowsOcrAsync(string pdfPath, CancellationToken ct)
        {
            try
            {
                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine is null)
                    return string.Empty;

                var file = await StorageFile.GetFileFromPathAsync(pdfPath);
                var pdf = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
                if (pdf.PageCount == 0)
                    return string.Empty;

                var sb = new StringBuilder();

                for (uint i = 0; i < pdf.PageCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    using var page = pdf.GetPage(i);
                    using var stream = new InMemoryRandomAccessStream();
                    await page.RenderToStreamAsync(stream);

                    stream.Seek(0);
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    var result = await engine.RecognizeAsync(bitmap);

                    if (!string.IsNullOrWhiteSpace(result.Text))
                        sb.AppendLine(result.Text);
                }

                return sb.ToString().Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<string> ExtractImageWithWindowsOcrAsync(string imagePath, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine is null)
                    return string.Empty;

                var file = await StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenReadAsync();

                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var result = await engine.RecognizeAsync(bitmap);

                return result.Text?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<byte[]> RenderPageAsPngAsync(Windows.Data.Pdf.PdfPage page)
        {
            using var rendered = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(rendered);
            rendered.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(rendered);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            using var pngStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            pngStream.Seek(0);
            var size = (uint)pngStream.Size;
            if (size == 0)
                return Array.Empty<byte>();

            using var reader = new DataReader(pngStream.GetInputStreamAt(0));
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }

        private static bool IsLikelyUsefulText(string text, int threshold = 20)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var informativeChars = text.Count(char.IsLetterOrDigit);
            return informativeChars >= threshold;
        }

        // ── Paso 2: Estructuración con IA ─────────────────────────────────────

        public static async Task<List<ImportedProductDto>> StructureWithAiAsync(
            string rawText,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return new List<ImportedProductDto>();

            var systemPrompt = AiSettingsService.LoadPdfExtractPrompt();
            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = "Extrae los productos del texto y devuelve un array JSON: [{\"nombre\":\"\",\"precio\":0,\"unidad\":\"unidad\"}]";

            var route = await AiProviderRouter.Instance.StructureProductsAsync(systemPrompt, rawText, ct);
            if (!route.Success)
                throw new InvalidOperationException($"No se pudo estructurar productos con IA: {route.Error}");

            var parsed = ParseProductList(route.Content);
            if (parsed.Count == 0)
                throw new InvalidOperationException("La IA no devolvió productos válidos para importar.");

            return parsed;
        }

        internal static List<ImportedProductDto> ParseProductList(string json)
        {
            var result = new List<ImportedProductDto>();
            try
            {
                // Limpiar posibles bloques ```json ```
                var clean = json.Trim();
                if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    clean = clean["```json".Length..];
                if (clean.StartsWith("```"))
                    clean = clean[3..];
                if (clean.EndsWith("```"))
                    clean = clean[..^3];
                clean = clean.Trim();

                using var doc = JsonDocument.Parse(clean);
                var root = doc.RootElement;

                // El modelo podría devolver el array envuelto en un objeto
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            root = prop.Value;
                            break;
                        }
                    }
                }

                foreach (var item in root.EnumerateArray())
                {
                    result.Add(new ImportedProductDto
                    {
                        Nombre = GetStr(item, "nombre"),
                        Precio = GetDbl(item, "precio"),
                        Unidad = GetStr(item, "unidad", "unidad")
                    });
                }
            }
            catch { /* JSON malformado — devolvemos lista vacía */ }

            return result.Where(p => !string.IsNullOrWhiteSpace(p.Nombre)).ToList();
        }

        // ── Paso 3: Deduplicación ─────────────────────────────────────────────

        public static ImportResult Deduplicate(List<ImportedProductDto> imported)
        {
            var existing = DatabaseService.GetProducts();

            var newItems       = new List<ImportedProductDto>();
            var exactDupes     = new List<ImportedProductDto>();
            var priceChanged   = new List<(Product, ImportedProductDto)>();

            foreach (var dto in imported)
            {
                var match = FindClosestProduct(dto.Nombre, existing);

                if (match == null)
                {
                    newItems.Add(dto);
                }
                else if (Math.Abs(match.UnitPrice - dto.Precio) < 0.001)
                {
                    exactDupes.Add(dto);
                }
                else
                {
                    priceChanged.Add((match, dto));
                }
            }

            return new ImportResult(newItems, exactDupes, priceChanged);
        }

        internal static Product? FindClosestProduct(string name, List<Product> products)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // Coincidencia exacta (insensible a mayúsculas)
            var exact = products.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Levenshtein con umbral ≤ 3
            var best     = (Product?)null;
            var bestDist = int.MaxValue;

            foreach (var p in products)
            {
                var dist = Levenshtein(name.ToLowerInvariant(), p.Name.ToLowerInvariant());
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }

            return bestDist <= 3 ? best : null;
        }

        // ── Levenshtein ───────────────────────────────────────────────────────

        internal static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }

            return d[a.Length, b.Length];
        }

        // ── Commit ────────────────────────────────────────────────────────────

        /// <summary>Guarda en la DB los ítems seleccionados por el usuario en ImportReviewWindow.</summary>
        public static void CommitImport(
            List<ImportedProductDto> toAdd,
            List<(Product existing, ImportedProductDto incoming)> toUpdate)
        {
            foreach (var dto in toAdd)
            {
                DatabaseService.AddProduct(new Product
                {
                    Sku       = GenerateSku(dto.Nombre),
                    Name      = dto.Nombre,
                    Category  = "Importado",
                    Unit      = dto.Unidad,
                    UnitPrice = dto.Precio
                });
            }

            foreach (var (existing, incoming) in toUpdate)
            {
                DatabaseService.UpdateProductPrice(existing.Id, incoming.Precio);
            }
        }

        private static string GenerateSku(string name)
        {
            var parts = name.Split(' ').Take(3);
            return string.Join("-", parts.Select(p => p.Length >= 3 ? p[..3].ToUpperInvariant() : p.ToUpperInvariant()));
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string GetStr(JsonElement el, string prop, string fallback = "")
        {
            return el.TryGetProperty(prop, out var v) ? v.GetString() ?? fallback : fallback;
        }

        private static double GetDbl(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return 0;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDouble(),
                JsonValueKind.String => double.TryParse(v.GetString(), out var d) ? d : 0,
                _ => 0
            };
        }
    }
}
