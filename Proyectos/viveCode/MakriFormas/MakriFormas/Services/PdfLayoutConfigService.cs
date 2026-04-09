using System;
using System.Globalization;
using System.IO;

namespace MakriFormas.Services
{
    public sealed class PdfLayoutConfig
    {
        // Core elements requested for manual positioning.
        public float TitleX { get; set; } = 154f;
        public float TitleY { get; set; } = 158f;
        public float TitleFontSize { get; set; } = 14f;

        public float NameX { get; set; } = 160f;
        public float NameY { get; set; } = 124f;
        public float NameFontSize { get; set; } = 9f;

        public float RucX { get; set; } = 160f;
        public float RucY { get; set; } = 140f;
        public float RucFontSize { get; set; } = 9f;

        public float DateLabelX { get; set; } = 180f;
        public float DateLabelY { get; set; } = 35f;
        public float DateLabelFontSize { get; set; } = 10f;

        public float DateValueX { get; set; } = 180f;
        public float DateValueY { get; set; } = 37f;
        public float DateValueFontSize { get; set; } = 10f;

        public float TableX { get; set; } = 154f;
        public float TableY { get; set; } = 168f;
        public float TableWidth { get; set; } = 352f;
        public float TableHeight { get; set; } = 248f;

        public float TableHeaderX { get; set; } = 154f;
        public float TableHeaderY { get; set; } = 162f;
        public float TableHeaderFontSize { get; set; } = 8f;

        public float TableBodyFontSize { get; set; } = 8f;
        public float TableRowHeight { get; set; } = 15f;
        public int TableMaxRows { get; set; } = 15;

        public float TableQtyWidth { get; set; } = 38f;
        public float TableDescWidth { get; set; } = 190f;
        public float TableUnitWidth { get; set; } = 58f;
        public float TableTotalWidth { get; set; } = 66f;

        public float TotalX { get; set; } = 154f;
        public float TotalY { get; set; } = 418f;
        public float TotalFontSize { get; set; } = 8f;

        public float DeliveryX { get; set; } = 176f;
        public float DeliveryY { get; set; } = 430f;
        public float DeliveryFontSize { get; set; } = 8f;

        public float ValidityX { get; set; } = 176f;
        public float ValidityY { get; set; } = 435f;
        public float ValidityFontSize { get; set; } = 8f;

        public float TableBorderThickness { get; set; } = 0.5f;

        public void Normalize()
        {
            TitleFontSize = Clamp(TitleFontSize, 6f, 72f);
            NameFontSize = Clamp(NameFontSize, 6f, 72f);
            RucFontSize = Clamp(RucFontSize, 6f, 72f);
            DateLabelFontSize = Clamp(DateLabelFontSize, 6f, 72f);
            DateValueFontSize = Clamp(DateValueFontSize, 6f, 72f);
            TableHeaderFontSize = Clamp(TableHeaderFontSize, 6f, 72f);
            TableBodyFontSize = Clamp(TableBodyFontSize, 6f, 72f);
            TotalFontSize = Clamp(TotalFontSize, 6f, 72f);
            DeliveryFontSize = Clamp(DeliveryFontSize, 6f, 72f);
            ValidityFontSize = Clamp(ValidityFontSize, 6f, 72f);

            TableWidth = Math.Max(120f, TableWidth);
            TableHeight = Math.Max(80f, TableHeight);
            TableRowHeight = Math.Max(8f, TableRowHeight);
            TableBorderThickness = Clamp(TableBorderThickness, 0.2f, 2f);

            TableQtyWidth = Math.Max(20f, TableQtyWidth);
            TableDescWidth = Math.Max(40f, TableDescWidth);
            TableUnitWidth = Math.Max(30f, TableUnitWidth);
            TableTotalWidth = Math.Max(30f, TableTotalWidth);
            TableMaxRows = Math.Clamp(TableMaxRows, 1, 40);
        }

        private static float Clamp(float value, float min, float max)
            => Math.Min(max, Math.Max(min, value));
    }

    public static class PdfLayoutConfigService
    {
        public static PdfLayoutConfig Load()
        {
            var config = new PdfLayoutConfig();
            var path = GetConfigPath();

            if (!File.Exists(path))
                WriteDefault(path, config);

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line.Substring(0, separator).Trim().ToLowerInvariant();
                var value = line.Substring(separator + 1).Trim();

                ApplyValue(config, key, value);
            }

            config.Normalize();
            return config;
        }

        public static string GetConfigPath()
        {
            var candidates = GetCandidatePaths();
            var existing = candidates
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (existing.Count > 0)
            {
                return existing
                    .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                    .First();
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    var folder = Path.GetDirectoryName(candidate);
                    if (!string.IsNullOrWhiteSpace(folder))
                        Directory.CreateDirectory(folder);

                    WriteDefault(candidate, new PdfLayoutConfig());
                    return candidate;
                }
                catch
                {
                    // Try next candidate.
                }
            }

            throw new FileNotFoundException("No fue posible ubicar ni crear pdf_layout.conf.");
        }

        private static string[] GetCandidatePaths()
        {
            return new[]
            {
                Path.Combine(Environment.CurrentDirectory, "pdf_layout.conf"),
                Path.Combine(Environment.CurrentDirectory, "config", "pdf_layout.conf"),
                Path.Combine(AppContext.BaseDirectory, "pdf_layout.conf"),
                Path.Combine(AppContext.BaseDirectory, "config", "pdf_layout.conf"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "pdf_layout.conf")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "pdf_layout.conf"))
            };
        }

        public static void EnsureConfigFileExists()
        {
            _ = GetConfigPath();
        }

        private static void ApplyValue(PdfLayoutConfig config, string key, string value)
        {
            if (key == "table.maxrows")
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows))
                    config.TableMaxRows = rows;

                return;
            }

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return;

            switch (key)
            {
                case "title.x": config.TitleX = number; break;
                case "title.y": config.TitleY = number; break;
                case "title.fontsize": config.TitleFontSize = number; break;

                case "name.x": config.NameX = number; break;
                case "name.y": config.NameY = number; break;
                case "name.fontsize": config.NameFontSize = number; break;

                case "ruc.x": config.RucX = number; break;
                case "ruc.y": config.RucY = number; break;
                case "ruc.fontsize": config.RucFontSize = number; break;

                case "date.label.x": config.DateLabelX = number; break;
                case "date.label.y": config.DateLabelY = number; break;
                case "date.label.fontsize": config.DateLabelFontSize = number; break;

                case "date.value.x": config.DateValueX = number; break;
                case "date.value.y": config.DateValueY = number; break;
                case "date.value.fontsize": config.DateValueFontSize = number; break;

                case "table.x": config.TableX = number; break;
                case "table.y": config.TableY = number; break;
                case "table.width": config.TableWidth = number; break;
                case "table.height": config.TableHeight = number; break;

                case "table.header.x": config.TableHeaderX = number; break;
                case "table.header.y": config.TableHeaderY = number; break;
                case "table.header.fontsize": config.TableHeaderFontSize = number; break;

                case "table.body.fontsize": config.TableBodyFontSize = number; break;
                case "table.rowheight": config.TableRowHeight = number; break;

                case "table.qtywidth": config.TableQtyWidth = number; break;
                case "table.descwidth": config.TableDescWidth = number; break;
                case "table.unitwidth": config.TableUnitWidth = number; break;
                case "table.totalwidth": config.TableTotalWidth = number; break;

                case "total.x": config.TotalX = number; break;
                case "total.y": config.TotalY = number; break;
                case "total.fontsize": config.TotalFontSize = number; break;

                case "delivery.x": config.DeliveryX = number; break;
                case "delivery.y": config.DeliveryY = number; break;
                case "delivery.fontsize": config.DeliveryFontSize = number; break;

                case "validity.x": config.ValidityX = number; break;
                case "validity.y": config.ValidityY = number; break;
                case "validity.fontsize": config.ValidityFontSize = number; break;

                case "table.borderthickness": config.TableBorderThickness = number; break;
            }
        }

        private static void WriteDefault(string path, PdfLayoutConfig config)
        {
            var content = BuildDefaultConfigText(config);
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(path, content);
        }

        private static string BuildDefaultConfigText(PdfLayoutConfig c)
        {
            string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

            return string.Join(Environment.NewLine, new[]
            {
                "# MakriFormas PDF layout configuration",
                "# Units: points (A4 PDF)",
                "# IMPORTANT: x and y are listed first for each element.",
                "",
                "# --- Titulo ---",
                $"title.x={F(c.TitleX)}",
                $"title.y={F(c.TitleY)}",
                $"title.fontSize={F(c.TitleFontSize)}",
                "",
                "# --- Nombre ---",
                $"name.x={F(c.NameX)}",
                $"name.y={F(c.NameY)}",
                $"name.fontSize={F(c.NameFontSize)}",
                "",
                "# --- RUC ---",
                $"ruc.x={F(c.RucX)}",
                $"ruc.y={F(c.RucY)}",
                $"ruc.fontSize={F(c.RucFontSize)}",
                "",
                "# --- Fecha (etiqueta y valor) ---",
                $"date.label.x={F(c.DateLabelX)}",
                $"date.label.y={F(c.DateLabelY)}",
                $"date.label.fontSize={F(c.DateLabelFontSize)}",
                $"date.value.x={F(c.DateValueX)}",
                $"date.value.y={F(c.DateValueY)}",
                $"date.value.fontSize={F(c.DateValueFontSize)}",
                "",
                "# --- Tabla ---",
                $"table.x={F(c.TableX)}",
                $"table.y={F(c.TableY)}",
                $"table.width={F(c.TableWidth)}",
                $"table.height={F(c.TableHeight)}",
                $"table.header.x={F(c.TableHeaderX)}",
                $"table.header.y={F(c.TableHeaderY)}",
                $"table.header.fontSize={F(c.TableHeaderFontSize)}",
                $"table.body.fontSize={F(c.TableBodyFontSize)}",
                $"table.rowHeight={F(c.TableRowHeight)}",
                $"table.maxRows={c.TableMaxRows}",
                $"table.qtyWidth={F(c.TableQtyWidth)}",
                $"table.descWidth={F(c.TableDescWidth)}",
                $"table.unitWidth={F(c.TableUnitWidth)}",
                $"table.totalWidth={F(c.TableTotalWidth)}",
                $"table.borderThickness={F(c.TableBorderThickness)}",
                "",
                "# --- Total ---",
                $"total.x={F(c.TotalX)}",
                $"total.y={F(c.TotalY)}",
                $"total.fontSize={F(c.TotalFontSize)}",
                "",
                "# --- Fecha entrega ---",
                $"delivery.x={F(c.DeliveryX)}",
                $"delivery.y={F(c.DeliveryY)}",
                $"delivery.fontSize={F(c.DeliveryFontSize)}",
                "",
                "# --- Validez ---",
                $"validity.x={F(c.ValidityX)}",
                $"validity.y={F(c.ValidityY)}",
                $"validity.fontSize={F(c.ValidityFontSize)}",
                ""
            });
        }
    }
}
