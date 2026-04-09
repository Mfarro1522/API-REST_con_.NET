using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MakriFormas.Services;
using MakriFormas.Views;

namespace MakriFormas
{
    /// <summary>
    /// Shell principal de la aplicación. Contiene:
    /// - Sidebar de navegación (oscura)
    /// - ContentControl central (intercambia vistas)
    /// - Drawer de chat IA a la derecha
    /// </summary>
    public partial class MainWindow : Window
    {
        // ── Chat state ────────────────────────────────────────────────────────
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        private CancellationTokenSource? _cts;
        private string? _attachedFilePath;
        private const int MaxAttachmentContextChars = 20_000;
        private bool _chatDrawerOpen = false;

        // ── Current nav views (kept alive for state) ─────────────────────────
        private DashboardView?         _dashboardView;
        private NewProformaView?       _proformaView;
        private InventoryView?         _inventoryView;
        private ProformaHistoryView?   _historyView;

        // ── Nav button references for active-state toggling ───────────────────
        private Button? _activeNavBtn;

        public MainWindow()
        {
                    InitializeComponent();
            icMessages.ItemsSource = Messages;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Show dashboard as default view
            NavigateTo("dashboard");

            // Load AI status
            _ = CheckAiStatusAsync();

            // Welcome message for chat
            AddAgentMessage("¡Hola! Soy el asistente de MakriFormas 🤖\n" +
                            "Puedo ayudarte a gestionar productos, crear proformas y más.\n" +
                            "También puedes adjuntar una imagen o PDF para analizar su contenido.\n" +
                            "¿En qué te ayudo hoy?");
        }

        // ══════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ══════════════════════════════════════════════════════════════════════

        private void NavigateTo(string section)
        {
            switch (section)
            {
                case "dashboard":
                    if (_dashboardView == null)
                    {
                        _dashboardView = new DashboardView();
                        _dashboardView.NavigationRequested += NavigateTo;
                    }
                    _dashboardView.Refresh();
                    MainContent.Content = _dashboardView;
                    SetHeader("Dashboard", "Panel principal");
                    SetActiveNav(btnNavDashboard);
                    break;

                case "proforma":
                    if (_proformaView == null)
                        _proformaView = new NewProformaView();
                    MainContent.Content = _proformaView;
                    SetHeader("Nueva Proforma", "Crear cotización de materiales");
                    SetActiveNav(btnNavNewProforma);
                    break;

                case "inventory":
                    if (_inventoryView == null)
                        _inventoryView = new InventoryView();
                    _inventoryView.RefreshData();
                    MainContent.Content = _inventoryView;
                    SetHeader("Inventario", "Gestión de productos y stock");
                    SetActiveNav(btnNavInventory);
                    break;

                case "history":
                    if (_historyView == null)
                    {
                        _historyView = new ProformaHistoryView();
                        _historyView.NavigationRequested += NavigateTo;
                    }
                    _historyView.LoadHistory();
                    MainContent.Content = _historyView;
                    SetHeader("Historial", "Registro de cotizaciones guardadas");
                    SetActiveNav(btnNavHistory);
                    break;
            }
        }

        private void SetHeader(string title, string subtitle)
        {
            txtCurrentSection.Text    = title;
            txtCurrentSectionSub.Text = subtitle;
        }

        private void SetActiveNav(Button? targetBtn)
        {
            // Reset previous active
            if (_activeNavBtn != null)
                _activeNavBtn.Style = (Style)FindResource("NavItem");

            _activeNavBtn = targetBtn;

            if (_activeNavBtn != null)
                _activeNavBtn.Style = (Style)FindResource("NavItemActive");
        }

        // ── Nav button click handlers ──────────────────────────────────────────

        private void NavDashboard_Click(object sender, RoutedEventArgs e)   => NavigateTo("dashboard");
        private void NavNewProforma_Click(object sender, RoutedEventArgs e) => NavigateTo("proforma");
        private void NavHistory_Click(object sender, RoutedEventArgs e)     => NavigateTo("history");
        private void NavInventory_Click(object sender, RoutedEventArgs e)   => NavigateTo("inventory");

        // ── Tool handlers ──────────────────────────────────────────────────────

        private void OpenAiSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new AiProviderSettingsWindow { Owner = this };
            win.ShowDialog();
            _ = CheckAiStatusAsync(); // refresh status after settings change
        }

        private void OpenImportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Proformas compatibles (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|PDF (*.pdf)|*.pdf|Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title  = "Seleccionar proforma (PDF o imagen)"
            };

            if (dialog.ShowDialog() == true)
            {
                var importWindow = new ImportReviewWindow(dialog.FileName) { Owner = this };
                importWindow.ImportConfirmed += (_, _) =>
                {
                    // Refresh dashboard and inventory if visible
                    _dashboardView?.Refresh();
                    _inventoryView?.RefreshData();
                };
                importWindow.ShowDialog();
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseTransferService.ImportDatabase(this))
            {
                _dashboardView?.Refresh();
                _inventoryView?.RefreshData();
            }
        }

        private void ExportDatabase_Click(object sender, RoutedEventArgs e)
            => DatabaseTransferService.ExportDatabase(this);

        // ══════════════════════════════════════════════════════════════════════
        // CHAT DRAWER
        // ══════════════════════════════════════════════════════════════════════

        private void ToggleChatDrawer_Click(object sender, RoutedEventArgs e)
        {
            _chatDrawerOpen = !_chatDrawerOpen;
            colChatDrawer.Width = _chatDrawerOpen
                ? new GridLength(380)
                : new GridLength(0);

            btnToggleChat.Content = _chatDrawerOpen ? "✕  Cerrar chat" : "🤖  Asistente";
            btnNavAgent.Style = _chatDrawerOpen
                ? (Style)FindResource("NavItemActive")
                : (Style)FindResource("NavItem");
        }

        // ── Send message ───────────────────────────────────────────────────────

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
            => await SendMessageAsync();

        private async void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtInputPlaceholder != null)
                txtInputPlaceholder.Visibility = string.IsNullOrEmpty(txtInput.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task SendMessageAsync()
        {
            var text = txtInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(_attachedFilePath)) return;

            var userDisplay = text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_attachedFilePath))
            {
                var fileName = Path.GetFileName(_attachedFilePath);
                userDisplay = string.IsNullOrWhiteSpace(userDisplay)
                    ? $"Adjunto: {fileName}"
                    : $"{userDisplay}\n\nAdjunto: {fileName}";
            }

            txtInput.Text = string.Empty;
            btnSend.IsEnabled   = false;
            btnAttach.IsEnabled = false;

            Messages.Add(new ChatMessage { IsUser = true, Content = userDisplay });
            ScrollToBottom();

            var agentMsg = new ChatMessage { IsUser = false, Content = string.Empty };
            Messages.Add(agentMsg);
            pnlThinking.Visibility = Visibility.Visible;
            ScrollToBottom();

            _cts = new CancellationTokenSource();

            try
            {
                var outboundMessage = text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_attachedFilePath))
                {
                    txtThinking.Text = "⏳ Analizando archivo adjunto...";
                    var context = await BuildAttachmentContextAsync(_attachedFilePath, _cts.Token);
                    outboundMessage = ComposeUserMessageWithAttachment(text ?? string.Empty, _attachedFilePath, context);
                }

                txtThinking.Text = "⏳ Pensando...";
                var accum = new System.Text.StringBuilder();

                var response = await AgentDispatcher.ProcessStreamAsync(
                    outboundMessage,
                    token =>
                    {
                        accum.Append(token);
                        Dispatcher.Invoke(() =>
                        {
                            pnlThinking.Visibility = Visibility.Collapsed;
                            agentMsg.Content = accum.ToString();
                            ScrollToBottom();
                        });
                    },
                    ct: _cts.Token);

                if (!string.IsNullOrWhiteSpace(response.Message))
                    agentMsg.Content = response.Message;

                // Update AI status indicator
                if (response.UsedFallback)
                {
                    elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    txtAiStatus.Text = "Respaldo Groq activo";
                    if (!string.IsNullOrWhiteSpace(response.ProviderNotice))
                        AddAgentMessage($"Aviso: {response.ProviderNotice}");
                }
                else if (!string.IsNullOrWhiteSpace(response.ProviderUsed))
                {
                    elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(6, 148, 148));
                    txtAiStatus.Text = $"Activo: {response.ProviderUsed}";
                }

                // Agent action: create proforma
                if (response.Data is ProformaPreFillData prefill)
                {
                    // Navigate to proforma view and pre-fill it
                    NavigateTo("proforma");
                    _proformaView?.ResetForNew();
                    _proformaView?.PreFill(prefill);
                }

                // Agent changed DB (added product, etc.)
                if (response.DbChanged)
                {
                    _dashboardView?.Refresh();
                    _inventoryView?.RefreshData();
                }
            }
            catch (OperationCanceledException)
            {
                agentMsg.Content = "(Cancelado)";
            }
            catch (Exception ex)
            {
                agentMsg.Content = $"Error inesperado: {ex.Message}";
            }
            finally
            {
                pnlThinking.Visibility  = Visibility.Collapsed;
                btnSend.IsEnabled       = true;
                btnAttach.IsEnabled     = true;
                _cts?.Dispose();
                _cts = null;
                ClearAttachment();
                ScrollToBottom();
            }
        }

        // ── Chat helpers ───────────────────────────────────────────────────────

        private void AddAgentMessage(string text)
            => Messages.Add(new ChatMessage { IsUser = false, Content = text });

        private void ScrollToBottom() => scrollChat.ScrollToBottom();

        private async Task<string> BuildAttachmentContextAsync(string filePath, CancellationToken ct)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff"))
                return "[Formato no compatible para análisis]";

            var extracted = await PdfImportService.ExtractTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(extracted))
                return "[No se pudo extraer texto del archivo]";

            if (extracted.Length > MaxAttachmentContextChars)
                extracted = extracted[..MaxAttachmentContextChars] + "\n\n[Contenido recortado]";

            return extracted;
        }

        private static string ComposeUserMessageWithAttachment(string userText, string filePath, string extractedText)
        {
            var fileName    = Path.GetFileName(filePath);
            var instruction = string.IsNullOrWhiteSpace(userText)
                ? "Analiza el archivo adjunto y responde en español."
                : userText;

            return $"{instruction}\n\n[ARCHIVO_ADJUNTO]\nNombre: {fileName}\nContenido:\n{extractedText}\n[/ARCHIVO_ADJUNTO]";
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos compatibles (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title  = "Seleccionar archivo"
            };

            if (dialog.ShowDialog() == true)
            {
                _attachedFilePath       = dialog.FileName;
                txtAttachment.Text      = $"Adjunto: {Path.GetFileName(_attachedFilePath)}";
                txtAttachment.Visibility        = Visibility.Visible;
                btnClearAttachment.Visibility   = Visibility.Visible;
            }
        }

        private void ClearAttachment_Click(object sender, RoutedEventArgs e) => ClearAttachment();

        private void ClearAttachment()
        {
            _attachedFilePath           = null;
            txtAttachment.Text          = string.Empty;
            txtAttachment.Visibility        = Visibility.Collapsed;
            btnClearAttachment.Visibility   = Visibility.Collapsed;
        }

        private Task CheckAiStatusAsync()
        {
            var hasGoogleKey = !string.IsNullOrWhiteSpace(SecureSecretsService.GetGoogleApiKey());
            var hasGroqKey   = !string.IsNullOrWhiteSpace(SecureSecretsService.GetGroqApiKey());
            var options      = AiSettingsService.GetOptions();

            txtModelInfo.Text = $"Google: {options.GoogleChatModel} | Groq: {options.GroqChatModel}";

            if (hasGoogleKey)
            {
                elAiStatus.Fill  = new SolidColorBrush(Color.FromRgb(6, 148, 148));
                txtAiStatus.Text = hasGroqKey ? "IA lista ✓" : "Google listo (sin respaldo)";
            }
            else
            {
                elAiStatus.Fill  = new SolidColorBrush(Color.FromRgb(200, 80, 80));
                txtAiStatus.Text = "Falta Google API key";
            }

            return Task.CompletedTask;
        }
    }
}