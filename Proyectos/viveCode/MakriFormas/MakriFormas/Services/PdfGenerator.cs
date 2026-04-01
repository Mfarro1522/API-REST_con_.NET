using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MakriFormas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MakriFormas.Services
{
    public static class PdfGenerator
    {
        public static void GenerateProformaPdf(string filePath, string clientName, string ruc, DateTime? date, IEnumerable<ProformaItem> items, double total)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var templateBytes = File.ReadAllBytes(ResolveTemplatePath());
            var printableItems = items?.Where(x => x.Quantity > 0).ToList() ?? new List<ProformaItem>();
            var documentDate = DateTime.Now;
            var deliveryDate = date?.ToString("dd/MM/yyyy") ?? "Por coordinar";
            var safeClientName = SanitizeClientName(clientName);
            var safeRuc = string.IsNullOrWhiteSpace(ruc) ? "-" : ruc.Trim();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0, Unit.Point);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily(Fonts.Arial));

                    page.Background().Image(templateBytes).FitArea();

                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Column(col =>
                        {
                            col.Item()
                                .PaddingTop(Layout.DateLabelY)
                                .PaddingLeft(Layout.DateLabelX)
                                .Text("Fecha :")
                                .FontSize(18)
                                .SemiBold();

                            col.Item()
                                .PaddingTop(Layout.DateGapY)
                                .PaddingLeft(Layout.DateX)
                                .Text(documentDate.ToString("dd/MM/yyyy"))
                                .FontSize(18)
                                .SemiBold();

                            col.Item()
                                .PaddingTop(Layout.ClientGapY)
                                .PaddingLeft(Layout.ClientX)
                                .Text($"Nombre: {safeClientName}")
                                .FontSize(9);

                            col.Item()
                                .PaddingTop(Layout.RucGapY)
                                .PaddingLeft(Layout.RucX)
                                .Text($"RUC: {safeRuc}")
                                .FontSize(9);

                            col.Item()
                                .PaddingTop(Layout.ProformaGapY)
                                .PaddingLeft(Layout.TableX)
                                .Width(Layout.TableWidth)
                                .AlignCenter()
                                .Text("PROFORMA")
                                .FontSize(22)
                                .SemiBold()
                                .Underline();

                            col.Item()
                                .PaddingTop(Layout.HeaderGapY)
                                .PaddingLeft(Layout.TableX)
                                .Width(Layout.TableWidth)
                                .Row(row =>
                                {
                                    row.ConstantItem(Layout.QtyWidth).AlignCenter().Text("CANT.").FontSize(8).SemiBold();
                                    row.ConstantItem(Layout.DescWidth).AlignCenter().Text("DESCRIPCION").FontSize(8).SemiBold();
                                    row.ConstantItem(Layout.UnitWidth).AlignCenter().Text("P. UNIT").FontSize(8).SemiBold();
                                    row.ConstantItem(Layout.TotalWidth).AlignCenter().Text("P. TOTAL").FontSize(8).SemiBold();
                                });

                            col.Item()
                                .PaddingTop(Layout.TableGapY)
                                .PaddingLeft(Layout.TableX)
                                .Width(Layout.TableWidth)
                                .Height(Layout.TableHeight)
                                .Border(0.5f)
                                .BorderColor("#1A1A1A")
                                .Row(row =>
                                {
                                    row.ConstantItem(Layout.QtyWidth).Element(ColumnDividerStyle).Column(qtyCol =>
                                    {
                                        for (var i = 0; i < Layout.MaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            qtyCol.Item().Height(Layout.RowHeight)
                                                .AlignCenter().AlignMiddle()
                                                .Text(item?.Quantity.ToString() ?? string.Empty)
                                                .FontSize(8);
                                        }
                                    });

                                    row.ConstantItem(Layout.DescWidth).Element(ColumnDividerStyle).Column(descCol =>
                                    {
                                        for (var i = 0; i < Layout.MaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            descCol.Item().Height(Layout.RowHeight).PaddingLeft(2)
                                                .Text(BuildDescription(item))
                                                .FontSize(8)
                                                .ClampLines(2);
                                        }
                                    });

                                    row.ConstantItem(Layout.UnitWidth).Element(ColumnDividerStyle).Column(unitCol =>
                                    {
                                        for (var i = 0; i < Layout.MaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            unitCol.Item().Height(Layout.RowHeight).PaddingRight(2)
                                                .AlignRight().AlignMiddle()
                                                .Text(item != null ? $"S/ {item.UnitPrice:N2}" : string.Empty)
                                                .FontSize(8);
                                        }
                                    });

                                    row.ConstantItem(Layout.TotalWidth).PaddingRight(2).Column(totalCol =>
                                    {
                                        for (var i = 0; i < Layout.MaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            totalCol.Item().Height(Layout.RowHeight)
                                                .AlignRight().AlignMiddle()
                                                .Text(item != null ? $"S/ {item.Total:N2}" : string.Empty)
                                                .FontSize(8);
                                        }
                                    });
                                });

                            col.Item()
                                .PaddingTop(Layout.TotalGapY)
                                .PaddingLeft(Layout.TableX)
                                .Width(Layout.TableWidth)
                                .AlignRight()
                                .Text($"S/ {total:N2}")
                                .SemiBold();

                            col.Item()
                                .PaddingTop(Layout.DeliveryGapY)
                                .PaddingLeft(Layout.DeliveryX)
                                .Text($"Fecha de entrega: {deliveryDate}")
                                .FontSize(8);

                            col.Item()
                                .PaddingTop(Layout.ValidityGapY)
                                .PaddingLeft(Layout.ValidityX)
                                .Text("Validez: 10 dias")
                                .FontSize(8);
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        private static string ResolveTemplatePath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "proformaBase.png"),
                Path.Combine(Environment.CurrentDirectory, "proformaBase.png"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "proformaBase.png"))
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("No se encontro la plantilla proformaBase.png.");
        }

        private static IContainer ColumnDividerStyle(IContainer container)
        {
            return container
            .BorderRight(0.5f)
            .BorderColor("#1A1A1A");
        }

        private static string BuildDescription(ProformaItem? item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var description = item.Description ?? string.Empty;
            if (item.IsAreaBased)
            {
                description += $" {item.Width:N2}x{item.Height:N2}m";
            }

            return description;
        }

        private static string SanitizeClientName(string? clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
            {
                return "-";
            }

            var trimmed = clientName.Trim();
            if (trimmed.Equals("Razón Social / Nombre", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Razon Social / Nombre", StringComparison.OrdinalIgnoreCase))
            {
                return "-";
            }

            return trimmed;
        }

        // Ajusta estas coordenadas para calibrar el contenido sobre la plantilla.
        private static class Layout
        {
            public const float DateLabelX = 180;
            public const float DateLabelY = 35;

            public const float DateX = 180;
            public const float DateGapY = 2;

            public const float ClientX = 160;
            public const float ClientGapY = 87;

            public const float RucX = 160;
            public const float RucGapY = 16;

            public const float ProformaGapY = 18;
            public const float HeaderGapY = 4;

            public const float TableX = 154;
            public const float TableGapY = 6;
            public const float TableWidth = 352;
            public const float TableHeight = 248;
            public const float RowHeight = 15;
            public const int MaxRows = 15;

            public const float QtyWidth = 38;
            public const float DescWidth = 190;
            public const float UnitWidth = 58;
            public const float TotalWidth = 66;

            public const float TotalX = 448;
            public const float TotalGapY = 2;

            public const float DeliveryX = 176;
            public const float DeliveryGapY = 12;

            public const float ValidityX = 176;
            public const float ValidityGapY = 5;
        }
    }
}