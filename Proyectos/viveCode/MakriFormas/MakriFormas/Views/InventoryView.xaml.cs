using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas.Views
{
    public partial class InventoryView : UserControl
    {
        private List<Product> products = new();
        private bool showOnlyLowStock;
        private bool sortAscending = true;

        public InventoryView()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshData();
        }

        public void RefreshData()
        {
            products = DatabaseService.GetProducts();
            txtActiveProducts.Text = products.Count.ToString();
            txtTotalStock.Text = products.Sum(p => p.StockQuantity).ToString("N2");
            txtCategories.Text = products.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString();
            txtLowStock.Text = $"{products.Count(p => p.StockQuantity <= 5)} ítems";
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<Product> filtered = products;

            var query = txtInventorySearch?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
                filtered = filtered.Where(p =>
                    p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (showOnlyLowStock)
                filtered = filtered.Where(p => p.StockQuantity <= 5);

            filtered = sortAscending
                ? filtered.OrderBy(p => p.Name)
                : filtered.OrderByDescending(p => p.Name);

            var list = filtered.ToList();
            dgProducts.ItemsSource = list;
            txtProductsSummary.Text = $"Mostrando {list.Count} de {products.Count} producto(s)";

            if (btnFilterLowStock != null)
                btnFilterLowStock.Content = showOnlyLowStock ? "✅  Quitar Filtro" : "⚠️  Filtrar Stock Bajo";

            if (btnToggleSort != null)
                btnToggleSort.Content = sortAscending ? "🔤  Ordenar A-Z" : "🔤  Ordenar Z-A";

            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (txtSearchPlaceholder != null && txtInventorySearch != null)
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtInventorySearch.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddProductWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                RefreshData();
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Product product)
            {
                var result = MessageBox.Show(
                    $"¿Estás seguro de que deseas eliminar '{product.Name}'?\nEsta acción no se puede deshacer.",
                    "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseService.DeleteProduct(product.Id);
                        RefreshData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseTransferService.ImportDatabase(Window.GetWindow(this)))
                RefreshData();
        }

        private void ExportDatabase_Click(object sender, RoutedEventArgs e)
            => DatabaseTransferService.ExportDatabase(Window.GetWindow(this));

        private void InventorySearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilters();

        private void FilterLowStock_Click(object sender, RoutedEventArgs e)
        {
            showOnlyLowStock = !showOnlyLowStock;
            ApplyFilters();
        }

        private void ToggleSort_Click(object sender, RoutedEventArgs e)
        {
            sortAscending = !sortAscending;
            ApplyFilters();
        }
    }
}
