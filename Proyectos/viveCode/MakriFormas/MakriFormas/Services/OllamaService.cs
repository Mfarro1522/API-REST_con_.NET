using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    /// <summary>
    /// Cliente HTTP para Ollama local (http://localhost:11434).
    /// Soporte para streaming token-a-token y respuesta única.
    /// </summary>
    public sealed class OllamaService : IDisposable
    {
        // Modelos del chatbot (edita estos dos valores segun tu despliegue)
        public static string ChatPrimaryModel  { get; set; } = "qwen3.5:cloud";
        public static string ChatFallbackModel { get; set; } = "qwen2.5:7b";

        private static readonly Lazy<OllamaService> _instance = new(() => new OllamaService());
        public static OllamaService Instance => _instance.Value;

        private readonly HttpClient _http;
        private const string BaseUrl = "http://localhost:11434";

        private OllamaService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(180)
            };
        }

        // ── Streaming (IAsyncEnumerable) ─────────────────────────────────────

        /// <summary>
        /// Envía un mensaje al modelo y devuelve tokens uno a uno (streaming).
        /// </summary>
        public async IAsyncEnumerable<string> ChatStreamAsync(
            string systemPrompt,
            string userMessage,
            string model = "qwen2.5:7b",
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var body = BuildRequestBody(systemPrompt, userMessage, model, stream: true);

            HttpResponseMessage? response = null;
            string? earlyError = null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    var err = ExtractErrorMessage(errBody);
                    earlyError = $"[MODEL_ERROR] {(int)response.StatusCode} - {err}";
                }
            }
            catch (HttpRequestException)
            {
                earlyError = "[MODEL_ERROR] Ollama no está corriendo. Inicia Ollama y vuelve a intentarlo.";
            }
            catch (TaskCanceledException)
            {
                earlyError = "[MODEL_ERROR] La solicitud tomó demasiado tiempo. Verifica que el modelo esté cargado.";
            }

            if (earlyError != null)
            {
                yield return earlyError;
                yield break;
            }

            using var stream = await response!.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? token = null;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp) &&
                        msgProp.TryGetProperty("content", out var contentProp))
                    {
                        token = contentProp.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Línea NDJSON malformada, ignorar
                }

                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }

        // ── Respuesta única (para extracción de PDF) ─────────────────────────

        /// <summary>
        /// Envía un mensaje y espera la respuesta completa (sin streaming).
        /// </summary>
        public async Task<string> ChatOnceAsync(
            string systemPrompt,
            string userMessage,
            string model = "qwen2.5:7b",
            CancellationToken ct = default)
        {
            var body = BuildRequestBody(systemPrompt, userMessage, model, stream: false);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                var response = await _http.SendAsync(request, ct);
                var json = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var err = ExtractErrorMessage(json);
                    return BuildAgentErrorJson($"[MODEL_ERROR] {(int)response.StatusCode} - {err}");
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (HttpRequestException)
            {
                return BuildAgentErrorJson("[MODEL_ERROR] Ollama no está corriendo. Por favor inicia Ollama.");
            }
            catch (TaskCanceledException)
            {
                return BuildAgentErrorJson("[MODEL_ERROR] La solicitud al modelo tomó demasiado tiempo.");
            }
        }

        /// <summary>
        /// Envía una imagen al modelo multimodal y devuelve la respuesta completa (sin streaming).
        /// </summary>
        public async Task<string> ChatImageOnceAsync(
            string systemPrompt,
            string userMessage,
            byte[] imageBytes,
            string model = "glm-ocr:q8_0",
            CancellationToken ct = default)
        {
            var imageBase64 = Convert.ToBase64String(imageBytes);
            var body = BuildImageRequestBody(systemPrompt, userMessage, imageBase64, model);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (HttpRequestException)
            {
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                return string.Empty;
            }
        }

        // ── Context utils ────────────────────────────────────────────────────

        /// <summary>
        /// Lee el system prompt del archivo txt e inyecta el contexto de unidades.
        /// </summary>
        public static string LoadSystemPromptWithUnits()
        {
            var promptPath = ResolvePromptPath("agent_system.txt");
            var unitsPath = ResolveConfigPath("unidades_negocio.json");

            var prompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : FallbackSystemPrompt();

            if (File.Exists(unitsPath))
            {
                var unitsJson = File.ReadAllText(unitsPath);
                // Formato legible para el modelo
                var unitsSummary = BuildUnitsSummary(unitsJson);
                prompt = prompt.Replace("{UNIDADES_PLACEHOLDER}", unitsSummary);
            }
            else
            {
                prompt = prompt.Replace("{UNIDADES_PLACEHOLDER}", "unidad, m2, metro, kg, oz");
            }

            return prompt;
        }

        /// <summary>
        /// Lee el system prompt de extracción de PDF.
        /// </summary>
        public static string LoadPdfExtractPrompt()
        {
            var path = ResolvePromptPath("pdf_extract_system.txt");
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string BuildRequestBody(string systemPrompt, string userMessage, string model, bool stream)
        {
            var obj = new
            {
                model,
                stream,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userMessage }
                }
            };
            return JsonSerializer.Serialize(obj);
        }

        private static string BuildImageRequestBody(string systemPrompt, string userMessage, string imageBase64, string model)
        {
            var obj = new
            {
                model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage, images = new[] { imageBase64 } }
                }
            };

            return JsonSerializer.Serialize(obj);
        }

        public static List<string> GetChatModelCandidates(string preferredModel = "")
        {
            return new[] { preferredModel, ChatPrimaryModel, ChatFallbackModel }
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildAgentErrorJson(string message)
        {
            var obj = new { action = "chat", @params = new { }, message };
            return JsonSerializer.Serialize(obj);
        }

        private static string ExtractErrorMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "error desconocido";

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                    return err.GetString() ?? "error desconocido";

                if (root.TryGetProperty("message", out var msg))
                {
                    if (msg.ValueKind == JsonValueKind.String)
                        return msg.GetString() ?? "error desconocido";

                    if (msg.ValueKind == JsonValueKind.Object &&
                        msg.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString() ?? "error desconocido";
                    }
                }
            }
            catch
            {
                // Si no es JSON, devolvemos el texto en bruto.
            }

            return raw.Length > 240 ? raw[..240] : raw;
        }

        private static string BuildUnitsSummary(string unitsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(unitsJson);
                var sb = new StringBuilder();
                foreach (var unit in doc.RootElement.GetProperty("unidades").EnumerateArray())
                {
                    var clave   = unit.TryGetProperty("clave",    out var c) ? c.GetString() : "?";
                    var calculo = unit.TryGetProperty("calculo",  out var ca) ? ca.GetString() : "";
                    var ejemplo = unit.TryGetProperty("ejemplo",  out var ej) ? ej.GetString() : "";
                    sb.AppendLine($"- {clave}: cálculo = {calculo}, ejemplo = {ejemplo}");
                }
                return sb.ToString();
            }
            catch
            {
                return "unidad, m2, metro, kg, oz";
            }
        }

        private static string ResolvePromptPath(string filename)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "prompts", filename),
                Path.Combine(Environment.CurrentDirectory, "prompts", filename),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts", filename))
            };
            foreach (var p in candidates)
                if (File.Exists(p)) return p;
            return candidates[0];
        }

        private static string ResolveConfigPath(string filename)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config", filename),
                Path.Combine(Environment.CurrentDirectory, "config", filename),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", filename))
            };
            foreach (var p in candidates)
                if (File.Exists(p)) return p;
            return candidates[0];
        }

        private static string FallbackSystemPrompt() =>
            "Eres el asistente de MakriFormas. Responde SIEMPRE con JSON: {\"action\":\"chat\",\"params\":{},\"message\":\"...\"}";

        public void Dispose() => _http.Dispose();
    }
}
