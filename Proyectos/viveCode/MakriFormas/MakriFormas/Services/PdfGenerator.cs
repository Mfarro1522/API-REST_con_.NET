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

            var layout = PdfLayoutConfigService.Load();
            var effectiveTableWidth = Math.Max(layout.TableWidth,
                layout.TableQtyWidth + layout.TableDescWidth + layout.TableUnitWidth + layout.TableTotalWidth);

            var templateBytes = File.ReadAllBytes(ResolveTemplatePath());
            var printableItems = items?.Where(x => x.Cantidad > 0).ToList() ?? new List<ProformaItem>();
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
                    page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial));

                    page.Background().Image(templateBytes).FitArea();

                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Element(container =>
                        {
                            container
                                .PaddingLeft(layout.DateLabelX)
                                .PaddingTop(layout.DateLabelY)
                                .Text("Fecha :")
                                .FontSize(layout.DateLabelFontSize)
                                .SemiBold();
                        });

                        DrawLayerElement(layers, layout.DateValueX, layout.DateValueY, container =>
                        {
                            container
                                .Text(documentDate.ToString("dd/MM/yyyy"))
                                .FontSize(layout.DateValueFontSize)
                                .SemiBold();
                        });

                        DrawLayerElement(layers, layout.NameX, layout.NameY, container =>
                        {
                            container
                                .Text($"Nombre: {safeClientName}")
                                .FontSize(layout.NameFontSize);
                        });

                        DrawLayerElement(layers, layout.RucX, layout.RucY, container =>
                        {
                            container
                                .Text($"RUC: {safeRuc}")
                                .FontSize(layout.RucFontSize);
                        });

                        DrawLayerElement(layers, layout.TitleX, layout.TitleY, container =>
                        {
                            container
                                .Width(effectiveTableWidth)
                                .AlignCenter()
                                .Text("PROFORMA")
                                .FontSize(layout.TitleFontSize)
                                .SemiBold()
                                .Underline();
                        });

                        DrawLayerElement(layers, layout.TableHeaderX, layout.TableHeaderY, container =>
                        {
                            container
                                .Width(effectiveTableWidth)
                                .Row(row =>
                                {
                                    row.ConstantItem(layout.TableQtyWidth).AlignCenter().Text("CANT.").FontSize(layout.TableHeaderFontSize).SemiBold();
                                    row.ConstantItem(layout.TableDescWidth).AlignCenter().Text("DESCRIPCION").FontSize(layout.TableHeaderFontSize).SemiBold();
                                    row.ConstantItem(layout.TableUnitWidth).AlignCenter().Text("P. UNIT").FontSize(layout.TableHeaderFontSize).SemiBold();
                                    row.ConstantItem(layout.TableTotalWidth).AlignCenter().Text("P. TOTAL").FontSize(layout.TableHeaderFontSize).SemiBold();
                                });
                        });

                        DrawLayerElement(layers, layout.TableX, layout.TableY, container =>
                        {
                            container
                                .Width(effectiveTableWidth)
                                .Height(layout.TableHeight)
                                .Border(layout.TableBorderThickness)
                                .BorderColor("#1A1A1A")
                                .Row(row =>
                                {
                                    row.ConstantItem(layout.TableQtyWidth).Element(ColumnDividerStyle).Column(qtyCol =>
                                    {
                                        for (var i = 0; i < layout.TableMaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            qtyCol.Item()
                                                .Height(layout.TableRowHeight)
                                                .AlignCenter().AlignMiddle()
                                                .Text(item != null ? FormatQuantity(item) : string.Empty)
                                                .FontSize(layout.TableBodyFontSize);
                                        }
                                    });

                                    row.ConstantItem(layout.TableDescWidth).Element(ColumnDividerStyle).Column(descCol =>
                                    {
                                        for (var i = 0; i < layout.TableMaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            descCol.Item().PaddingLeft(2)
                                                .Height(layout.TableRowHeight)
                                                .AlignMiddle()
                                                .Text(BuildDescription(item))
                                                .FontSize(layout.TableBodyFontSize)
                                                .ClampLines(2);
                                        }
                                    });

                                    row.ConstantItem(layout.TableUnitWidth).Element(ColumnDividerStyle).Column(unitCol =>
                                    {
                                        for (var i = 0; i < layout.TableMaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            unitCol.Item().PaddingRight(2)
                                                .Height(layout.TableRowHeight)
                                                .AlignRight().AlignMiddle()
                                                .Text(item != null ? $"S/ {item.UnitPrice:N2}" : string.Empty)
                                                .FontSize(layout.TableBodyFontSize);
                                        }
                                    });

                                    row.ConstantItem(layout.TableTotalWidth).PaddingRight(2).Column(totalCol =>
                                    {
                                        for (var i = 0; i < layout.TableMaxRows; i++)
                                        {
                                            var item = i < printableItems.Count ? printableItems[i] : null;
                                            totalCol.Item()
                                                .Height(layout.TableRowHeight)
                                                .AlignRight().AlignMiddle()
                                                .Text(item != null ? $"S/ {item.Total:N2}" : string.Empty)
                                                .FontSize(layout.TableBodyFontSize);
                                        }
                                    });
                                });
                        });

                        DrawLayerElement(layers, layout.TotalX, layout.TotalY, container =>
                        {
                            container
                                .Width(effectiveTableWidth)
                                .AlignRight()
                                .Text($"S/ {total:N2}")
                                .FontSize(layout.TotalFontSize)
                                .SemiBold();
                        });

                        DrawLayerElement(layers, layout.DeliveryX, layout.DeliveryY, container =>
                        {
                            container
                                .Text($"Fecha de entrega: {deliveryDate}")
                                .FontSize(layout.DeliveryFontSize);
                        });

                        DrawLayerElement(layers, layout.ValidityX, layout.ValidityY, container =>
                        {
                            container
                                .Text("Validez: 10 dias")
                                .FontSize(layout.ValidityFontSize);
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        private static void DrawLayerElement(LayersDescriptor layers, float x, float y, Action<IContainer> draw)
        {
            layers.Layer().Element(container =>
            {
                var positioned = container
                    .PaddingLeft(x)
                    .PaddingTop(y);

                draw(positioned);
            });
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
            return item.Unidad switch
            {
                "m2"    => $"{description} {item.Ancho:N2}\u00d7{item.Alto:N2}m",
                "cm2"   => $"{description} {item.Ancho:N2}\u00d7{item.Alto:N2}cm",
                "metro" => $"{description} ({item.Longitud:N2}m)",
                "cm"    => $"{description} ({item.Longitud:N2}cm)",
                _       => description
            };
        }

        private static string FormatQuantity(ProformaItem item)
        {
            return item.Unidad switch
            {
                "m2"       => $"{item.Cantidad:N2} m\u00b2",
                "metro"    => $"{item.Cantidad:N2} m",
                "kg"       => $"{item.Cantidad:N2} kg",
                "millares" => $"{item.Cantidad:N0} millares",
                "cientos"  => $"{item.Cantidad:N0} cientos",
                "cm"       => $"{item.Cantidad:N2} cm",
                "cm2"      => $"{item.Cantidad:N2} cm\u00b2",
                _          => $"{item.Cantidad:N0} und"
            };
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
    }
}