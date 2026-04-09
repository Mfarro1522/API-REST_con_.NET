using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MakriFormas.Services;

namespace MakriFormas.Views
{
    public partial class DashboardView : UserControl
    {
        // Fired when the view wants to navigate somewhere else
        public event Action<string>? NavigationRequested;

        public DashboardView()
        {
            InitializeComponent();
            Loaded += (_, _) => Refresh();
        }

        public void Refresh()
        {
            var products = DatabaseService.GetProducts();

            txtDashboardProducts.Text = products.Count.ToString();
            txtDashboardCategories.Text = $"{products.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count()} categorías";
            txtDashboardStock.Text = products.Sum(p => p.StockQuantity).ToString("N2");

            var avg = products.Count == 0 ? 0 : products.Average(p => p.UnitPrice);
            txtDashboardAvgPrice.Text = $"Precio promedio: S/ {avg:N2}";

            dgRecentProducts.ItemsSource = products
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToList();

            // Proformas dinámicas
            var allProformas = DatabaseService.GetProformaHistory();
            var today = allProformas.Count(p => p.IssueDate.Date == DateTime.Today);
            txtDashboardProformasToday.Text = today.ToString();
            txtDashboardProformasSubtitle.Text = today == 1 ? "Creada hoy" : "Creadas hoy en total";
            txtDashboardProformaTotal.Text = allProformas.Count.ToString();
        }

        private void OpenNewProforma_Click(object sender, RoutedEventArgs e)
            => NavigationRequested?.Invoke("proforma");

        private void OpenInventory_Click(object sender, RoutedEventArgs e)
            => NavigationRequested?.Invoke("inventory");
    }
}
