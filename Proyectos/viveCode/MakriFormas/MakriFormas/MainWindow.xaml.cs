using System;
using System.Linq;
using System.Windows;
using MakriFormas.Services;

namespace MakriFormas
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDashboard();
        }

        private void RefreshDashboard()
        {
            var products = DatabaseService.GetProducts();

            txtDashboardProducts.Text = products.Count.ToString();
            txtDashboardCategories.Text = $"{products.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count()} categorías";
            txtDashboardStock.Text = products.Sum(p => p.StockQuantity).ToString("N2");

            var averagePrice = products.Count == 0 ? 0 : products.Average(p => p.UnitPrice);
            txtDashboardAvgPrice.Text = $"Precio promedio: S/ {averagePrice:N2}";

            dgRecentProducts.ItemsSource = products
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToList();
        }

        private void OpenNewProforma_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowOrActivateProforma(this);
            }
        }

        private void OpenInventory_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowOrActivateInventory(this);
            }
        }

        private void OpenProformaHistory_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowOrActivateProformaHistory(this);
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseTransferService.ImportDatabase(this))
            {
                RefreshDashboard();
            }
        }

        private void ExportDatabase_Click(object sender, RoutedEventArgs e)
        {
            DatabaseTransferService.ExportDatabase(this);
        }
    }
}