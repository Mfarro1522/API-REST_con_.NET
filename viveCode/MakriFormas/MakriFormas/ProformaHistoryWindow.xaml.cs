using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas
{
    public partial class ProformaHistoryWindow : Window
    {
        private List<ProformaHistoryEntry> historyRows = new();
        private ProformaDraft? selectedProforma;

        public ProformaHistoryWindow()
        {
            InitializeComponent();
            Loaded += ProformaHistoryWindow_Loaded;
        }

        private void ProformaHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void LoadHistory()
        {
            historyRows = DatabaseService.GetProformaHistory();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<ProformaHistoryEntry> filtered = historyRows;

            var query = txtHistorySearch?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(p =>
                    p.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.ClientName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Ruc.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.ToList();
            dgProformaHistory.ItemsSource = list;
            txtHistorySummary.Text = $"Mostrando {list.Count} de {historyRows.Count} proforma(s)";

            UpdateSearchPlaceholder();
        }

        private void UpdateSearchPlaceholder()
        {
            if (txtSearchPlaceholder != null && txtHistorySearch != null)
            {
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtHistorySearch.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void HistorySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void OpenNewProforma_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowOrActivateProforma(null);
                Close();
            }
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // --- Detail Panel ---

        private void ProformaHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProformaHistory.SelectedItem is ProformaHistoryEntry entry)
            {
                LoadProformaDetail(entry.Id);
            }
        }

        private void ProformaHistory_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgProformaHistory.SelectedItem is ProformaHistoryEntry entry)
            {
                LoadProformaDetail(entry.Id);
            }
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

                // Fill detail panel
                txtDetailCode.Text = draft.Code;
                txtDetailClient.Text = string.IsNullOrWhiteSpace(draft.ClientName) ? "-" : draft.ClientName;
                txtDetailRuc.Text = string.IsNullOrWhiteSpace(draft.Ruc) ? "-" : draft.Ruc;
                txtDetailAddress.Text = string.IsNullOrWhiteSpace(draft.Address) ? "-" : draft.Address;
                txtDetailIssueDate.Text = draft.IssueDate.ToString("dd/MM/yyyy");
                txtDetailDeliveryDate.Text = draft.DeliveryDate?.ToString("dd/MM/yyyy") ?? "Por coordinar";
                txtDetailTotal.Text = $"S/ {draft.Total:N2}";

                // Items
                icDetailItems.ItemsSource = draft.Items;

                // Observations
                if (!string.IsNullOrWhiteSpace(draft.DesignerObservations))
                {
                    panelDesignerObs.Visibility = Visibility.Visible;
                    txtDetailDesigner.Text = draft.DesignerObservations;
                }
                else
                {
                    panelDesignerObs.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(draft.InstallerObservations))
                {
                    panelInstallerObs.Visibility = Visibility.Visible;
                    txtDetailInstaller.Text = draft.InstallerObservations;
                }
                else
                {
                    panelInstallerObs.Visibility = Visibility.Collapsed;
                }

                // Show detail panel
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
            colDetail.Width = new GridLength(380);
        }

        private void HideDetailPanel()
        {
            borderDetail.Visibility = Visibility.Collapsed;
            colDetail.Width = new GridLength(0);
            selectedProforma = null;
        }

        private void CloseDetail_Click(object sender, RoutedEventArgs e)
        {
            HideDetailPanel();
        }

        private void ExportSelectedPdf_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProforma == null)
            {
                MessageBox.Show("Selecciona una proforma primero.", "Exportar PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = $"Proforma_{selectedProforma.Code.Replace("-", "_")}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    PdfGenerator.GenerateProformaPdf(
                        saveFileDialog.FileName,
                        selectedProforma.ClientName,
                        selectedProforma.Ruc,
                        selectedProforma.DeliveryDate,
                        selectedProforma.Items,
                        selectedProforma.Total);

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

        private void DeleteProforma_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProformaHistoryEntry entry)
            {
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar la proforma '{entry.Code}'?\n\nEsta acción no se puede deshacer.",
                                             "Confirmar Eliminación",
                                             MessageBoxButton.YesNo,
                                             MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseService.DeleteProforma(entry.Id);
                        LoadHistory();
                        
                        if (selectedProforma?.Code == entry.Code)
                        {
                            HideDetailPanel();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar la proforma: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
