using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas
{
    /// <summary>
    /// Interaction logic for InventoryWindow.xaml
    /// </summary>
    public partial class InventoryWindow : Window
    {
        private List<Product> products = new();
        private bool showOnlyLowStock;
        private bool sortAscending = true;

        public InventoryWindow()
        {
            InitializeComponent();
            Loaded += InventoryWindow_Loaded;
        }

        private void InventoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void RefreshData()
        {
            products = DatabaseService.GetProducts();
            txtActiveProducts.Text = products.Count.ToString();
            txtTotalStock.Text = products.Sum(p => p.StockQuantity).ToString("N2");
            txtCategories.Text = products.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString();
            txtLowStock.Text = $"{products.Count(p => p.StockQuantity <= 5)} ítems";

            ApplyInventoryFilters();
        }

        private void ApplyInventoryFilters()
        {
            IEnumerable<Product> filtered = products;

            var query = txtInventorySearch?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(p =>
                    p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            if (showOnlyLowStock)
            {
                filtered = filtered.Where(p => p.StockQuantity <= 5);
            }

            filtered = sortAscending
                ? filtered.OrderBy(p => p.Name)
                : filtered.OrderByDescending(p => p.Name);

            var filteredList = filtered.ToList();
            dgProducts.ItemsSource = filteredList;
            txtProductsSummary.Text = $"Mostrando {filteredList.Count} de {products.Count} producto(s)";

            if (btnFilterLowStock != null)
            {
                btnFilterLowStock.Content = showOnlyLowStock ? "✅  Quitar Filtro" : "⚠️  Filtrar Stock Bajo";
            }

            if (btnToggleSort != null)
            {
                btnToggleSort.Content = sortAscending ? "🔤  Ordenar A-Z" : "🔤  Ordenar Z-A";
            }

            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (txtSearchPlaceholder != null && txtInventorySearch != null)
            {
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtInventorySearch.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void CloseAndReturn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenNewProforma_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is not App app)
            {
                return;
            }

            app.ShowOrActivateProforma(null);
            Close();
        }

        private void OpenProformaHistory_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is not App app)
            {
                return;
            }

            app.ShowOrActivateProformaHistory(null);
            Close();
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            var addProductWindow = new AddProductWindow
            {
                Owner = this
            };

            var wasSaved = addProductWindow.ShowDialog();
            if (wasSaved == true)
            {
                RefreshData();
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseTransferService.ImportDatabase(this))
            {
                RefreshData();
            }
        }

        private void ExportDatabase_Click(object sender, RoutedEventArgs e)
        {
            DatabaseTransferService.ExportDatabase(this);
        }

        private void InventorySearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyInventoryFilters();
        }

        private void FilterLowStock_Click(object sender, RoutedEventArgs e)
        {
            showOnlyLowStock = !showOnlyLowStock;
            ApplyInventoryFilters();
        }

        private void ToggleSort_Click(object sender, RoutedEventArgs e)
        {
            sortAscending = !sortAscending;
            ApplyInventoryFilters();
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is Product product)
            {
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar el producto '{product.Name}'? Esta acción no se puede deshacer.",
                                             "Confirmar Eliminación", 
                                             MessageBoxButton.YesNo, 
                                             MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseService.DeleteProduct(product.Id);
                        RefreshData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar el producto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
