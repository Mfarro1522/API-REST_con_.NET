using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas
{
    // ── ViewModels de fila ────────────────────────────────────────────────────

    public class NewProductRow : INotifyPropertyChanged
    {
        private bool isSelected = true;
        public bool IsSelected { get => isSelected; set { isSelected = value; OnPropertyChanged(); } }
        public string Nombre { get; init; } = string.Empty;
        public double Precio { get; init; }
        public string Unidad { get; init; } = "unidad";
        public string PrecioDisplay => $"S/ {Precio:N2}";

        public ImportedProductDto ToDto() => new() { Nombre = Nombre, Precio = Precio, Unidad = Unidad };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ChangedPriceRow : INotifyPropertyChanged
    {
        private bool isSelected = true;
        public bool IsSelected { get => isSelected; set { isSelected = value; OnPropertyChanged(); } }
        public int ProductId { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public double PrecioActual { get; init; }
        public double PrecioNuevo { get; init; }
        public string PrecioActualDisplay => $"S/ {PrecioActual:N2}";
        public string PrecioNuevoDisplay  => $"S/ {PrecioNuevo:N2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class DupeRow
    {
        public string Nombre { get; init; } = string.Empty;
        public double Precio { get; init; }
        public string PrecioDisplay => $"S/ {Precio:N2}";
    }

    // ── Ventana ───────────────────────────────────────────────────────────────

    public partial class ImportReviewWindow : Window
    {
        private readonly string _pdfPath;
        private readonly string _model;

        private List<NewProductRow>   _newRows     = new();
        private List<ChangedPriceRow> _changedRows = new();
        private List<DupeRow>         _dupeRows    = new();

        /// <summary>Se dispara si el usuario confirmó la importación.</summary>
        public event EventHandler? ImportConfirmed;

        public ImportReviewWindow(string pdfPath, string model = "qwen2.5:1.5b")
        {
            InitializeComponent();
            _pdfPath = pdfPath;
            _model   = model;

            Loaded += async (_, _) => await ProcessPdfAsync();
        }

        // ── Procesamiento ─────────────────────────────────────────────────────

        private async Task ProcessPdfAsync()
        {
            pnlProgress.Visibility = Visibility.Visible;
            pnlContent.IsEnabled   = false;
            btnConfirm.IsEnabled   = false;

            try
            {
                UpdateProgress("Extrayendo texto del archivo...");
                var rawText = await PdfImportService.ExtractTextAsync(_pdfPath);

                UpdateProgress("Estructurando productos con IA...");
                var products = await PdfImportService.StructureWithAiAsync(rawText, _model);

                UpdateProgress("Deduplicando contra la base de datos...");
                var result = PdfImportService.Deduplicate(products);

                BindResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar el archivo: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                pnlProgress.Visibility = Visibility.Collapsed;
                pnlContent.IsEnabled   = true;
                btnConfirm.IsEnabled   = true;
            }
        }

        private void UpdateProgress(string message)
        {
            Dispatcher.Invoke(() => txtProgressStatus.Text = message);
        }

        private void BindResults(ImportResult result)
        {
            // Nuevos
            _newRows = result.New.Select(d => new NewProductRow
            {
                Nombre = d.Nombre, Precio = d.Precio, Unidad = d.Unidad
            }).ToList();
            dgNew.ItemsSource = _newRows;
            txtNewCount.Text  = $" ({_newRows.Count})";
            txtNoNew.Visibility = _newRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgNew.Visibility    = _newRows.Count > 0  ? Visibility.Visible : Visibility.Collapsed;

            // Precio cambiado
            _changedRows = result.PriceChanged.Select(pair => new ChangedPriceRow
            {
                ProductId    = pair.Existing.Id,
                Nombre       = pair.Existing.Name,
                PrecioActual = pair.Existing.UnitPrice,
                PrecioNuevo  = pair.Incoming.Precio
            }).ToList();
            dgChanged.ItemsSource  = _changedRows;
            txtChangedCount.Text   = $" ({_changedRows.Count})";
            txtNoChanged.Visibility = _changedRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgChanged.Visibility    = _changedRows.Count > 0  ? Visibility.Visible : Visibility.Collapsed;

            // Duplicados exactos
            _dupeRows = result.ExactDuplicates.Select(d => new DupeRow
            {
                Nombre = d.Nombre, Precio = d.Precio
            }).ToList();
            dgDupes.ItemsSource  = _dupeRows;
            txtDupeCount.Text    = $" ({_dupeRows.Count})";
            txtNoDupes.Visibility = _dupeRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgDupes.Visibility    = _dupeRows.Count > 0  ? Visibility.Visible : Visibility.Collapsed;

            // Resumen
            txtSummary.Text = $"{_newRows.Count} nuevos · {_changedRows.Count} actualizados · {_dupeRows.Count} sin cambios";
        }

        // ── Acciones ──────────────────────────────────────────────────────────

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var toAdd    = _newRows    .Where(r => r.IsSelected).Select(r => r.ToDto()).ToList();
            var toUpdate = _changedRows.Where(r => r.IsSelected)
                                       .Select(r => (
                                           new MakriFormas.Models.Product { Id = r.ProductId, Name = r.Nombre },
                                           new ImportedProductDto { Nombre = r.Nombre, Precio = r.PrecioNuevo }
                                       )).ToList();

            try
            {
                PdfImportService.CommitImport(toAdd, toUpdate);
                ImportConfirmed?.Invoke(this, EventArgs.Empty);

                MessageBox.Show(
                    $"Importación completada:\n  • {toAdd.Count} producto(s) agregado(s)\n  • {toUpdate.Count} precio(s) actualizado(s)",
                    "Importación exitosa", MessageBoxButton.OK, MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
