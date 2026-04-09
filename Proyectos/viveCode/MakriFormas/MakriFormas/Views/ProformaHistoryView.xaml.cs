using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas.Views
{
    public partial class ProformaHistoryView : UserControl
    {
        private List<ProformaHistoryEntry> historyRows = new();
        private ProformaDraft? selectedProforma;

        // Fired when user clicks "Nueva Proforma" from this view
        public event Action<string>? NavigationRequested;

        public ProformaHistoryView()
        {
            InitializeComponent();

            Loaded += (_, _) => LoadHistory();
        }

        public void LoadHistory()
        {
            historyRows = DatabaseService.GetProformaHistory();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<ProformaHistoryEntry> filtered = historyRows;

            var query = txtHistorySearch?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
                filtered = filtered.Where(p =>
                    p.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.ClientName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Ruc.Contains(query, StringComparison.OrdinalIgnoreCase));

            var list = filtered.ToList();
            dgProformaHistory.ItemsSource = list;
            txtHistorySummary.Text = $"Mostrando {list.Count} de {historyRows.Count} proforma(s)";
            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (txtSearchPlaceholder != null && txtHistorySearch != null)
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtHistorySearch.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HistorySearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadHistory();

        private void OpenNewProforma_Click(object sender, RoutedEventArgs e)
            => NavigationRequested?.Invoke("proforma");

        // â”€â”€ Detail panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ProformaHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProformaHistory.SelectedItem is ProformaHistoryEntry entry)
                LoadProformaDetail(entry.Id);
        }

        private void ProformaHistory_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgProformaHistory.SelectedItem is ProformaHistoryEntry entry)
                LoadProformaDetail(entry.Id);
        }

        private void LoadProformaDetail(int proformaId)
        {
            try
            {
                var draft = DatabaseService.GetProformaById(proformaId);
                if (draft == null)
                {
                    MessageBox.Show("No se pudo cargar la proforma seleccionada.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                selectedProforma = draft;

                txtDetailCode.Text          = draft.Code;
                txtDetailClient.Text        = string.IsNullOrWhiteSpace(draft.ClientName) ? "-" : draft.ClientName;
                txtDetailRuc.Text           = string.IsNullOrWhiteSpace(draft.Ruc) ? "-" : draft.Ruc;
                txtDetailAddress.Text       = string.IsNullOrWhiteSpace(draft.Address) ? "-" : draft.Address;
                txtDetailIssueDate.Text     = draft.IssueDate.ToString("dd/MM/yyyy");
                txtDetailDeliveryDate.Text  = draft.DeliveryDate?.ToString("dd/MM/yyyy") ?? "Por coordinar";
                txtDetailTotal.Text         = $"S/ {draft.Total:N2}";
                icDetailItems.ItemsSource   = draft.Items;

                var designerObservations = ProformaRichTextService.ToPlainText(draft.DesignerObservations);
                var installerObservations = ProformaRichTextService.ToPlainText(draft.InstallerObservations);

                panelDesignerObs.Visibility = string.IsNullOrWhiteSpace(designerObservations)
                    ? Visibility.Collapsed : Visibility.Visible;
                if (!string.IsNullOrWhiteSpace(designerObservations))
                    txtDetailDesigner.Text = designerObservations;

                panelInstallerObs.Visibility = string.IsNullOrWhiteSpace(installerObservations)
                    ? Visibility.Collapsed : Visibility.Visible;
                if (!string.IsNullOrWhiteSpace(installerObservations))
                    txtDetailInstaller.Text = installerObservations;

                ShowDetailPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar detalle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDetailPanel()
        {
            borderDetail.Visibility = Visibility.Visible;
            colDetail.Width = new GridLength(370);
        }

        private void HideDetailPanel()
        {
            borderDetail.Visibility = Visibility.Collapsed;
            colDetail.Width = new GridLength(0);
            selectedProforma = null;
        }

        private void CloseDetail_Click(object sender, RoutedEventArgs e) => HideDetailPanel();

        private void ExportSelectedPdf_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProforma == null)
            {
                MessageBox.Show("Selecciona una proforma primero.", "Exportar PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = $"Proforma_{selectedProforma.Code.Replace("-", "_")}"
                };

                if (dlg.ShowDialog() == true)
                {
                    PdfGenerator.GenerateProformaPdf(
                        dlg.FileName,
                        selectedProforma.ClientName,
                        selectedProforma.Ruc,
                        selectedProforma.DeliveryDate,
                        selectedProforma.Items,
                        selectedProforma.Total);

                    MessageBox.Show("PDF exportado con Ã©xito.", "Ã‰xito", MessageBoxButton.OK, MessageBoxImage.Information);

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

        private void PreviewSelectedPdf_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProforma == null)
            {
                MessageBox.Show("Selecciona una proforma primero.", "Previsualizar PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Preview_{Guid.NewGuid():N}.pdf");

                PdfGenerator.GenerateProformaPdf(
                    tempFile,
                    selectedProforma.ClientName,
                    selectedProforma.Ruc,
                    selectedProforma.DeliveryDate,
                    selectedProforma.Items,
                    selectedProforma.Total);

                var previewWnd = new PdfPreviewWindow(tempFile);
                previewWnd.Owner = Window.GetWindow(this);
                previewWnd.ShowDialog();

                if (previewWnd.ExportConfirmed)
                {
                    ExportSelectedPdf_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OcurriÃ³ un error al generar la vista previa: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProforma_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProformaHistoryEntry entry)
            {
                var result = MessageBox.Show(
                    $"Â¿Eliminar la proforma '{entry.Code}'?\nEsta acciÃ³n no se puede deshacer.",
                    "Confirmar eliminaciÃ³n", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseService.DeleteProforma(entry.Id);
                        LoadHistory();
                        if (selectedProforma?.Id == entry.Id)
                            HideDetailPanel();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}

