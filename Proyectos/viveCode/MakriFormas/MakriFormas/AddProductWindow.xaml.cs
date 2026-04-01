using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas
{
    public partial class AddProductWindow : Window
    {
        private readonly string suggestedName;

        public AddProductWindow()
            : this(string.Empty)
        {
        }

        public AddProductWindow(string suggestedName)
        {
            InitializeComponent();
            this.suggestedName = suggestedName;

            if (!string.IsNullOrWhiteSpace(this.suggestedName))
            {
                txtName.Text = this.suggestedName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSku.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("SKU y nombre son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseNumber(txtPrice.Text, out var unitPrice) || unitPrice < 0)
            {
                MessageBox.Show("Ingresa un precio unitario válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseNumber(txtStock.Text, out var stock) || stock < 0)
            {
                MessageBox.Show("Ingresa un stock válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var category = (cboCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Materiales";
            var unit = (cboUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "und";

            var product = new Product
            {
                Sku = txtSku.Text,
                Name = txtName.Text,
                Category = category,
                StockQuantity = stock,
                Unit = unit,
                UnitPrice = unitPrice
            };

            DatabaseService.AddProduct(product);

            MessageBox.Show("Producto registrado con éxito", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool TryParseNumber(string? input, out double value)
        {
            var normalized = (input ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
