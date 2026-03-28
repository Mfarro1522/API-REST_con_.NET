using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using MakriFormas.Models;
using MakriFormas.Services;
using System.Collections.Generic;

namespace MakriFormas
{
    /// <summary>
    /// Interaction logic for NewProformaWindow.xaml
    /// </summary>
    public partial class NewProformaWindow : Window
    {
        public ObservableCollection<ProformaItem> Items { get; set; }
        private List<Product> catalogProducts = new();
        private int? currentProformaId;
        private bool hasUnsavedChanges;
        private bool suppressChangeTracking = true;
        private bool closeConfirmed;

        public NewProformaWindow()
        {
            InitializeComponent();
            Closing += NewProformaWindow_Closing;

            Items = new ObservableCollection<ProformaItem>();
            icItems.ItemsSource = Items;
            Items.CollectionChanged += Items_CollectionChanged;

            txtCliente.TextChanged += DocumentFieldChanged;
            txtRuc.TextChanged += DocumentFieldChanged;
            txtDireccion.TextChanged += DocumentFieldChanged;
            txtDesignerObservations.TextChanged += DocumentFieldChanged;
            txtInstallerObservations.TextChanged += DocumentFieldChanged;
            dpFecha.SelectedDateChanged += DocumentDateChanged;

            txtProformaCode.Text = DatabaseService.GetNextProformaCode();
            dpFecha.SelectedDate = DateTime.Today;

            LoadCatalogProducts();

            // Add initial empty item
            AddNewItem();
            suppressChangeTracking = false;
            hasUnsavedChanges = false;
        }

        private void LoadCatalogProducts()
        {
            catalogProducts = DatabaseService.GetProducts();
            lbProductMatches.ItemsSource = catalogProducts.Take(12).ToList();
            txtCatalogNoResults.Visibility = Visibility.Collapsed;
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ProformaItem item in e.NewItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (ProformaItem item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            UpdateTotal();
            MarkDocumentChanged();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProformaItem.Total))
            {
                UpdateTotal();
            }

            if (e.PropertyName != nameof(ProformaItem.Total))
            {
                MarkDocumentChanged();
            }
        }

        private void UpdateTotal()
        {
            double total = Items.Sum(i => i.Total);
            txtOverallTotal.Text = $"S/ {total:N2}";
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            AddNewItem();
        }

        private void AddNewItem()
        {
            Items.Add(new ProformaItem 
            { 
                Description = "Nuevo material...", 
                Quantity = 1, 
                UnitPrice = 0.00,
                Width = 1,
                Height = 1
            });
        }

        private void CatalogSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtCatalogSearch.Text?.Trim() ?? string.Empty;

            // Update placeholder visibility
            if (txtCatalogSearchPlaceholder != null)
            {
                txtCatalogSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtCatalogSearch.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

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
                .Take(12)
                .ToList();

            lbProductMatches.ItemsSource = results;
            txtCatalogNoResults.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ProductMatches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbProductMatches.SelectedItem is not Product product)
            {
                return;
            }

            Items.Add(new ProformaItem
            {
                Description = product.Name,
                Quantity = 1,
                UnitPrice = product.UnitPrice,
                Width = 1,
                Height = 1
            });

            lbProductMatches.SelectedItem = null;
        }

        private void RegisterMissingProduct_Click(object sender, RoutedEventArgs e)
        {
            var suggestedName = txtCatalogSearch.Text?.Trim() ?? string.Empty;

            var addProductWindow = new AddProductWindow(suggestedName)
            {
                Owner = this
            };

            var wasSaved = addProductWindow.ShowDialog();
            if (wasSaved == true)
            {
                LoadCatalogProducts();
                CatalogSearch_TextChanged(txtCatalogSearch, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!SaveCurrentProforma(showSuccessMessage: false))
                {
                    return;
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = "Proforma_" + DateTime.Now.ToString("yyyyMMdd")
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var total = Items.Sum(i => i.Total);
                    PdfGenerator.GenerateProformaPdf(
                        saveFileDialog.FileName,
                        txtCliente.Text,
                        txtRuc.Text,
                        dpFecha.SelectedDate,
                        Items,
                        total);

                    hasUnsavedChanges = false;

                    MessageBox.Show("PDF exportado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveFileDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseAndReturn_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmCloseIfNeeded())
            {
                return;
            }

            Close();
        }

        private void OpenInventory_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmCloseIfNeeded())
            {
                return;
            }

            if (Application.Current is App app)
            {
                app.ShowOrActivateInventory(null);
                Close();
            }
        }

        private void OpenHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmCloseIfNeeded())
            {
                return;
            }

            if (Application.Current is App app)
            {
                app.ShowOrActivateProformaHistory(null);
                Close();
            }
        }

        private void SaveProforma_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProforma(showSuccessMessage: true);
        }

        private void NewProformaWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (closeConfirmed || !hasUnsavedChanges)
            {
                return;
            }

            var result = MessageBox.Show(
                "Hay cambios sin guardar en la proforma. ¿Quieres salir de todas formas?",
                "Confirmar salida",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            closeConfirmed = true;
        }

        private bool ConfirmCloseIfNeeded()
        {
            if (!hasUnsavedChanges)
            {
                closeConfirmed = true;
                return true;
            }

            var result = MessageBox.Show(
                "Hay cambios sin guardar en la proforma. ¿Quieres salir de todas formas?",
                "Confirmar salida",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return false;
            }

            closeConfirmed = true;
            return true;
        }

        private bool SaveCurrentProforma(bool showSuccessMessage)
        {
            if (!TryBuildDraft(out var draft))
            {
                return false;
            }

            try
            {
                var savedId = DatabaseService.SaveProforma(draft);
                currentProformaId = savedId;

                hasUnsavedChanges = false;
                closeConfirmed = false;

                if (showSuccessMessage)
                {
                    MessageBox.Show("Proforma guardada correctamente.", "Guardar Proforma", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar la proforma: {ex.Message}", "Guardar Proforma", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool TryBuildDraft(out ProformaDraft draft)
        {
            draft = new ProformaDraft();

            var clientName = NormalizePlaceholder(txtCliente.Text, "Razón Social / Nombre");
            if (string.IsNullOrWhiteSpace(clientName))
            {
                MessageBox.Show("Ingresa el nombre del cliente antes de guardar.", "Guardar Proforma", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var validItems = Items
                .Where(i => i.Quantity > 0 && !string.IsNullOrWhiteSpace(i.Description))
                .Select(i => new ProformaItem
                {
                    Description = i.Description,
                    IsAreaBased = i.IsAreaBased,
                    Width = i.Width,
                    Height = i.Height,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                })
                .ToList();

            if (validItems.Count == 0)
            {
                MessageBox.Show("Agrega al menos un ítem válido antes de guardar.", "Guardar Proforma", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            draft = new ProformaDraft
            {
                Id = currentProformaId,
                Code = txtProformaCode.Text?.Trim() ?? string.Empty,
                IssueDate = DateTime.Today,
                DeliveryDate = dpFecha.SelectedDate,
                ClientName = clientName,
                Ruc = NormalizePlaceholder(txtRuc.Text, string.Empty),
                Address = NormalizePlaceholder(txtDireccion.Text, "Dirección de entrega"),
                DesignerObservations = txtDesignerObservations.Text?.Trim() ?? string.Empty,
                InstallerObservations = txtInstallerObservations.Text?.Trim() ?? string.Empty,
                Items = validItems
            };

            return true;
        }

        private static string NormalizePlaceholder(string? value, string placeholder)
        {
            var clean = (value ?? string.Empty).Trim();
            if (string.Equals(clean, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return clean;
        }

        private void DocumentFieldChanged(object sender, TextChangedEventArgs e)
        {
            MarkDocumentChanged();
        }

        private void DocumentDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            MarkDocumentChanged();
        }

        private void MarkDocumentChanged()
        {
            if (suppressChangeTracking)
            {
                return;
            }

            hasUnsavedChanges = true;
            closeConfirmed = false;
        }

        private void InputTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void InputTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }
    }
}
