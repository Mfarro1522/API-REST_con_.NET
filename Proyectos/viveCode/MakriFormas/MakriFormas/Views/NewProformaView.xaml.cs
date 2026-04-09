using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas.Views
{
    public partial class NewProformaView : UserControl
    {
        public ObservableCollection<ProformaItem> Items { get; set; }

        private List<Product> catalogProducts = new();
        private int? currentProformaId;
        private bool hasUnsavedChanges;
        private bool suppressChangeTracking = true;
        private bool syncingObservationControls;
        private RichTextBox? activeObservationEditor;

        public NewProformaView()
        {
            InitializeComponent();

            Items = new ObservableCollection<ProformaItem>();
            icItems.ItemsSource = Items;
            Items.CollectionChanged += Items_CollectionChanged;

            txtCliente.TextChanged += DocumentFieldChanged;
            txtRuc.TextChanged += DocumentFieldChanged;
            txtDireccion.TextChanged += DocumentFieldChanged;
            rtbDesignerObservations.TextChanged += ObservationEditor_TextChanged;
            rtbInstallerObservations.TextChanged += ObservationEditor_TextChanged;
            dpFecha.SelectedDateChanged += DocumentDateChanged;

            txtProformaCode.Text = DatabaseService.GetNextProformaCode();
            dpFecha.SelectedDate = DateTime.Today;

            InitializeObservationEditor();
            ClearObservationEditors();
            LoadCatalogProducts();
            UpdateItemCount();
            UpdateTotal();

            suppressChangeTracking = false;
            hasUnsavedChanges = false;
            UpdateSaveBadge();
        }

        /// <summary>Pre-fills the view with data from the AI agent.</summary>
        public void PreFill(ProformaPreFillData data)
        {
            suppressChangeTracking = true;

            if (!string.IsNullOrWhiteSpace(data.ClientName))
                txtCliente.Text = data.ClientName;
            if (!string.IsNullOrWhiteSpace(data.Ruc))
                txtRuc.Text = data.Ruc;

            if (data.Items.Count > 0)
                Items.Clear();

            foreach (var req in data.Items)
            {
                Items.Add(new ProformaItem
                {
                    Description = req.Description,
                    Unidad      = req.Unit,
                    Ancho       = req.Ancho    > 0 ? req.Ancho    : 1,
                    Alto        = req.Alto     > 0 ? req.Alto     : 1,
                    Longitud    = req.Longitud > 0 ? req.Longitud : 1,
                    Cantidad    = req.Cantidad,
                    UnitPrice   = req.Precio
                });
            }

            suppressChangeTracking = false;
            hasUnsavedChanges = false;
            UpdateItemCount();
            UpdateTotal();
            UpdateSaveBadge();
        }

        /// <summary>Resets the view so a new proforma can be created.</summary>
        public void ResetForNew()
        {
            suppressChangeTracking = true;

            txtCliente.Text = string.Empty;
            txtRuc.Text = string.Empty;
            txtDireccion.Text = string.Empty;
            ClearObservationEditors();
            dpFecha.SelectedDate = DateTime.Today;
            Items.Clear();
            currentProformaId = null;
            txtProformaCode.Text = DatabaseService.GetNextProformaCode();

            suppressChangeTracking = false;
            hasUnsavedChanges = false;
            UpdateSaveBadge();
            UpdateItemCount();
            UpdateTotal();
        }

        // ── Catalog ───────────────────────────────────────────────────────────

        private void LoadCatalogProducts()
        {
            catalogProducts = DatabaseService.GetProducts();
            lbProductMatches.ItemsSource = catalogProducts.Take(12).ToList();
            txtCatalogNoResults.Visibility = Visibility.Collapsed;
        }

        // ── Items collection events ───────────────────────────────────────────

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ProformaItem item in e.NewItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }

            if (e.OldItems != null)
                foreach (ProformaItem item in e.OldItems)
                    item.PropertyChanged -= Item_PropertyChanged;

            UpdateTotal();
            UpdateItemCount();
            MarkDocumentChanged();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProformaItem.Total))
                UpdateTotal();

            if (e.PropertyName != nameof(ProformaItem.Total))
                MarkDocumentChanged();
        }

        private void UpdateTotal()
        {
            double total = Items.Sum(i => i.Total);
            txtOverallTotal.Text = $"S/ {total:N2}";
        }

        private void UpdateItemCount()
        {
            txtItemCount.Text = $"{Items.Count} ítem(s)";
            emptyItemsState.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void AddProduct_Click(object sender, RoutedEventArgs e) => AddNewItem();

        private void AddNewItem()
        {
            Items.Add(new ProformaItem
            {
                Description = "",
                Unidad = "unidad",
                Cantidad = 1,
                UnitPrice = 0.00,
                Ancho = 1, Alto = 1, Longitud = 1
            });
        }

        private void ClearItems_Click(object sender, RoutedEventArgs e)
        {
            if (Items.Count == 0)
                return;

            var result = MessageBox.Show(
                "Se eliminarán todos los ítems de la proforma actual. ¿Deseas continuar?",
                "Limpiar ítems",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                Items.Clear();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProformaItem item)
                Items.Remove(item);
        }

        private void DuplicateItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not ProformaItem item)
                return;

            Items.Add(new ProformaItem
            {
                Description = item.Description,
                Unidad = item.Unidad,
                Ancho = item.Ancho,
                Alto = item.Alto,
                Longitud = item.Longitud,
                Cantidad = item.Cantidad,
                UnitPrice = item.UnitPrice
            });
        }

        private void CatalogSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtCatalogSearch.Text?.Trim() ?? string.Empty;

            if (txtCatalogSearchPlaceholder != null)
                txtCatalogSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtCatalogSearch.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(query))
            {
                lbProductMatches.ItemsSource = catalogProducts.Take(12).ToList();
                txtCatalogNoResults.Visibility = Visibility.Collapsed;
                return;
            }

            var results = catalogProducts
                .Where(p =>
                    p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(12).ToList();

            lbProductMatches.ItemsSource = results;
            txtCatalogNoResults.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CatalogSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (TryAddFirstSearchResult())
                e.Handled = true;
        }

        private void AddTopSearchResult_Click(object sender, RoutedEventArgs e)
        {
            TryAddFirstSearchResult();
        }

        private bool TryAddFirstSearchResult()
        {
            if (lbProductMatches.ItemsSource is not IEnumerable<Product> matches)
                return false;

            var firstMatch = matches.FirstOrDefault();
            if (firstMatch == null)
                return false;

            AddCatalogProductToItems(firstMatch);
            return true;
        }

        private void ProductMatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbProductMatches.SelectedItem is not Product product) return;

            AddCatalogProductToItems(product);

            lbProductMatches.SelectedItem = null;
        }

        private void AddCatalogProductToItems(Product product)
        {
            Items.Add(new ProformaItem
            {
                Description = product.Name,
                Unidad = product.Unit,
                Cantidad = 1,
                UnitPrice = product.UnitPrice,
                Ancho = 1,
                Alto = 1,
                Longitud = 1
            });
        }

        private void RegisterMissingProduct_Click(object sender, RoutedEventArgs e)
        {
            var name = txtCatalogSearch.Text?.Trim() ?? string.Empty;
            var win = new AddProductWindow(name)
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                LoadCatalogProducts();
                CatalogSearch_TextChanged(txtCatalogSearch, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            }
        }

        private void SaveProforma_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProforma(showSuccessMessage: true);
        }

        private void SaveAndNew_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveCurrentProforma(showSuccessMessage: false))
                return;

            ResetForNew();
            txtCliente.Focus();
            MessageBox.Show("Proforma guardada. Puedes crear una nueva de inmediato.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetForm_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Deseas limpiar igualmente el formulario?",
                    "Limpiar formulario",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            ResetForNew();
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!SaveCurrentProforma(showSuccessMessage: false)) return;

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = "Proforma_" + DateTime.Now.ToString("yyyyMMdd")
                };

                if (dlg.ShowDialog() == true)
                {
                    var total = Items.Sum(i => i.Total);
                    PdfGenerator.GenerateProformaPdf(
                        dlg.FileName,
                        txtCliente.Text,
                        txtRuc.Text,
                        dpFecha.SelectedDate,
                        Items,
                        total);

                    hasUnsavedChanges = false;
                    UpdateSaveBadge();

                    MessageBox.Show("PDF exportado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dlg.FileName, UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save logic ────────────────────────────────────────────────────────

        private bool SaveCurrentProforma(bool showSuccessMessage)
        {
            if (!TryBuildDraft(out var draft)) return false;

            try
            {
                var savedId = DatabaseService.SaveProforma(draft);
                currentProformaId = savedId;
                hasUnsavedChanges = false;
                UpdateSaveBadge();

                if (showSuccessMessage)
                    MessageBox.Show("Proforma guardada correctamente.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool TryBuildDraft(out ProformaDraft draft)
        {
            draft = new ProformaDraft();

            var clientName = txtCliente.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                MessageBox.Show("Ingresa el nombre del cliente antes de guardar.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var validItems = Items
                .Where(i => i.Cantidad > 0 && !string.IsNullOrWhiteSpace(i.Description))
                .Select(i => new ProformaItem
                {
                    Description = i.Description,
                    Unidad = i.Unidad,
                    Ancho = i.Ancho, Alto = i.Alto, Longitud = i.Longitud,
                    Cantidad = i.Cantidad,
                    UnitPrice = i.UnitPrice
                }).ToList();

            if (validItems.Count == 0)
            {
                MessageBox.Show("Agrega al menos un ítem válido antes de guardar.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            draft = new ProformaDraft
            {
                Id = currentProformaId,
                Code = txtProformaCode.Text?.Trim() ?? string.Empty,
                IssueDate = DateTime.Today,
                DeliveryDate = dpFecha.SelectedDate,
                ClientName = clientName,
                Ruc = txtRuc.Text?.Trim() ?? string.Empty,
                Address = txtDireccion.Text?.Trim() ?? string.Empty,
                DesignerObservations = ProformaRichTextService.Serialize(rtbDesignerObservations),
                InstallerObservations = ProformaRichTextService.Serialize(rtbInstallerObservations),
                Items = validItems
            };

            return true;
        }

        // ── Observation editor ──────────────────────────────────────────────

        private void InitializeObservationEditor()
        {
            cmbObservationFontFamily.ItemsSource = Fonts.SystemFontFamilies
                .OrderBy(f => f.Source)
                .ToList();

            rtbDesignerObservations.FontFamily = new FontFamily("Segoe UI");
            rtbInstallerObservations.FontFamily = new FontFamily("Segoe UI");
            rtbDesignerObservations.FontSize = 11;
            rtbInstallerObservations.FontSize = 11;

            activeObservationEditor = rtbDesignerObservations;
            syncingObservationControls = true;
            cmbObservationFontFamily.SelectedItem = rtbDesignerObservations.FontFamily;
            syncingObservationControls = false;
            RefreshObservationToolbarState();
        }

        private void ClearObservationEditors()
        {
            ProformaRichTextService.Deserialize(rtbDesignerObservations, string.Empty);
            ProformaRichTextService.Deserialize(rtbInstallerObservations, string.Empty);
        }

        private void ObservationEditor_TextChanged(object sender, TextChangedEventArgs e)
            => MarkDocumentChanged();

        private void ObservationRichTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBox editor)
            {
                activeObservationEditor = editor;
                RefreshObservationToolbarState();
            }
        }

        private void ObservationRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBox editor && editor == activeObservationEditor)
                RefreshObservationToolbarState();
        }

        private void ObservationFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (syncingObservationControls || cmbObservationFontFamily.SelectedItem is not FontFamily family)
                return;

            ApplyObservationFormat(TextElement.FontFamilyProperty, family);
        }

        private void ToggleBold_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
                ApplyObservationFormat(TextElement.FontWeightProperty, toggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal);
        }

        private void ToggleItalic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
                ApplyObservationFormat(TextElement.FontStyleProperty, toggle.IsChecked == true ? FontStyles.Italic : FontStyles.Normal);
        }

        private void ToggleUnderline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggle)
                return;

            var decoration = toggle.IsChecked == true
                ? TextDecorations.Underline
                : new TextDecorationCollection();

            ApplyObservationFormat(Inline.TextDecorationsProperty, decoration);
        }

        private void ApplyObservationFormat(DependencyProperty property, object value)
        {
            if (activeObservationEditor == null)
                return;

            activeObservationEditor.Focus();
            activeObservationEditor.Selection.ApplyPropertyValue(property, value);
            MarkDocumentChanged();
            RefreshObservationToolbarState();
        }

        private void RefreshObservationToolbarState()
        {
            if (activeObservationEditor == null)
                return;

            syncingObservationControls = true;

            var selection = activeObservationEditor.Selection;

            var fontFamilyObj = selection.GetPropertyValue(TextElement.FontFamilyProperty);
            if (fontFamilyObj is FontFamily fontFamily)
            {
                var familyInList = Fonts.SystemFontFamilies.FirstOrDefault(f => string.Equals(f.Source, fontFamily.Source, StringComparison.Ordinal));
                cmbObservationFontFamily.SelectedItem = familyInList ?? fontFamily;
            }

            var fontWeightObj = selection.GetPropertyValue(TextElement.FontWeightProperty);
            btnObsBold.IsChecked = fontWeightObj is FontWeight weight && weight == FontWeights.Bold;

            var fontStyleObj = selection.GetPropertyValue(TextElement.FontStyleProperty);
            btnObsItalic.IsChecked = fontStyleObj is FontStyle style && style == FontStyles.Italic;

            var decorationsObj = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            btnObsUnderline.IsChecked = decorationsObj is TextDecorationCollection collection &&
                                        collection.Any(d => d.Location == TextDecorationLocation.Underline);

            syncingObservationControls = false;
        }

        // ── Keyboard shortcuts ───────────────────────────────────────────────

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
            {
                e.Handled = true;
                SaveAndNew_Click(sender, new RoutedEventArgs());
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                e.Handled = true;
                SaveProforma_Click(sender, new RoutedEventArgs());
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
            {
                e.Handled = true;
                ExportPdf_Click(sender, new RoutedEventArgs());
            }
        }

        // ── Change tracking ───────────────────────────────────────────────────

        private void DocumentFieldChanged(object sender, TextChangedEventArgs e) => MarkDocumentChanged();
        private void DocumentDateChanged(object? sender, SelectionChangedEventArgs e) => MarkDocumentChanged();

        private void MarkDocumentChanged()
        {
            if (suppressChangeTracking) return;
            hasUnsavedChanges = true;
            UpdateSaveBadge();
        }

        private void UpdateSaveBadge()
        {
            if (badgeSaved == null || badgeUnsaved == null) return;

            if (hasUnsavedChanges)
            {
                badgeUnsaved.Visibility = Visibility.Visible;
                badgeSaved.Visibility   = Visibility.Collapsed;
            }
            else if (currentProformaId.HasValue)
            {
                badgeSaved.Visibility   = Visibility.Visible;
                badgeUnsaved.Visibility = Visibility.Collapsed;
            }
            else
            {
                badgeSaved.Visibility   = Visibility.Collapsed;
                badgeUnsaved.Visibility = Visibility.Collapsed;
            }
        }

        // ── TextBox focus helpers ─────────────────────────────────────────────

        private void InputTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private void InputTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
            }
        }

        private void ItemUnidad_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* binding updates model */ }
    }
}
