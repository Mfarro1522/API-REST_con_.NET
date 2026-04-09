using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MakriFormas.Services;

namespace MakriFormas
{
    // ── Modelo de mensaje de chat ─────────────────────────────────────────────

    public class ChatMessage : INotifyPropertyChanged
    {
        private string content = string.Empty;

        public bool IsUser  { get; init; }
        public bool IsAgent => !IsUser;

        public string Content
        {
            get => content;
            set { content = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Ventana de chat ──────────────────────────────────────────────────────

    public partial class AgentChatWindow : Window
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        /// <summary>Se dispara cuando el agente cambió algo en la DB.</summary>
        public event EventHandler? DbChanged;

        /// <summary>
        /// Se dispara cuando el agente quiere crear una proforma con datos pre-llenados.
        /// El suscriptor (MainWindow) debe navegar a la vista de proforma y llamar PreFill().
        /// </summary>
        public event EventHandler<ProformaPreFillData>? ProformaRequested;

        private CancellationTokenSource? _cts;
        private string? _attachedFilePath;
        private const int MaxAttachmentContextChars = 20000;

        public AgentChatWindow()
        {
            InitializeComponent();
            icMessages.ItemsSource = Messages;

            // Mensaje de bienvenida
            AddAgentMessage("¡Hola! Soy el asistente de MakriFormas 🤖\n" +
                            "Puedo ayudarte a gestionar productos, crear proformas y más.\n" +
                            "También puedes adjuntar una imagen o PDF para conversar sobre su contenido.\n" +
                            "¿En qué te ayudo hoy?");

            _ = CheckAiStatusAsync();
        }

        // ── Envío de mensajes ─────────────────────────────────────────────────

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

        private void TxtInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtInputPlaceholder != null)
                txtInputPlaceholder.Visibility = string.IsNullOrEmpty(txtInput.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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
            btnSend.IsEnabled = false;
            btnAttach.IsEnabled = false;

            // Burbuja del usuario
            Messages.Add(new ChatMessage { IsUser = true, Content = userDisplay });
            ScrollToBottom();

            // Placeholder del agente (se irá llenando con streaming)
            var agentMsg = new ChatMessage { IsUser = false, Content = string.Empty };
            Messages.Add(agentMsg);

            // Indicador "pensando"
            pnlThinking.Visibility = Visibility.Visible;
            ScrollToBottom();

            _cts = new CancellationTokenSource();

            try
            {
                var outboundMessage = text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_attachedFilePath))
                {
                    pnlThinking.Visibility = Visibility.Visible;
                    txtThinking.Text = "⏳ Analizando archivo adjunto...";

                    var attachmentContext = await BuildAttachmentContextAsync(_attachedFilePath, _cts.Token);
                    outboundMessage = ComposeUserMessageWithAttachment(
                        text ?? string.Empty,
                        _attachedFilePath,
                        attachmentContext);
                }

                txtThinking.Text = "⏳ Pensando...";

                var accum = new System.Text.StringBuilder();

                var response = await AgentDispatcher.ProcessStreamAsync(
                    outboundMessage,
                    token =>
                    {
                        accum.Append(token);
                        // Actualizar en el hilo de UI
                        Dispatcher.Invoke(() =>
                        {
                            pnlThinking.Visibility = Visibility.Collapsed;
                            agentMsg.Content = accum.ToString();
                            ScrollToBottom();
                        });
                    },
                    ct: _cts.Token);

                // Mostrar el mensaje limpio del agente (sin JSON crudo)
                if (!string.IsNullOrWhiteSpace(response.Message))
                {
                    agentMsg.Content = response.Message;
                }

                if (response.UsedFallback)
                {
                    Dispatcher.Invoke(() =>
                    {
                        elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        txtAiStatus.Text = "Respaldo Groq activo";
                    });

                    if (!string.IsNullOrWhiteSpace(response.ProviderNotice))
                    {
                        AddAgentMessage($"Aviso del sistema: {response.ProviderNotice}");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.ProviderUsed))
                {
                    Dispatcher.Invoke(() =>
                    {
                        elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(6, 148, 148));
                        txtAiStatus.Text = $"Activo: {response.ProviderUsed}";
                    });
                }

                // ── Acción: crear proforma ─────────────────────────────────────
                if (response.Data is ProformaPreFillData prefill)
                {
                    Dispatcher.Invoke(() => ProformaRequested?.Invoke(this, prefill));
                }

                // Si el agente cambió la DB directamente (add_product, etc.)
                if (response.DbChanged)
                {
                    DbChanged?.Invoke(this, EventArgs.Empty);
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
                pnlThinking.Visibility = Visibility.Collapsed;
                btnSend.IsEnabled = true;
                btnAttach.IsEnabled = true;
                _cts?.Dispose();
                _cts = null;
                ClearAttachment();
                ScrollToBottom();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void AddAgentMessage(string text)
        {
            Messages.Add(new ChatMessage { IsUser = false, Content = text });
        }

        private void ScrollToBottom()
        {
            scrollChat.ScrollToBottom();
        }

        private async Task<string> BuildAttachmentContextAsync(string filePath, CancellationToken ct)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var isSupported = ext is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff";

            if (!isSupported)
                return "[No se pudo analizar el adjunto: formato no compatible.]";

            var extracted = await PdfImportService.ExtractTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(extracted))
                return "[No se extrajo texto útil del archivo adjunto.]";

            if (extracted.Length > MaxAttachmentContextChars)
            {
                extracted = extracted[..MaxAttachmentContextChars] +
                    "\n\n[Contenido recortado por longitud para conversación.]";
            }

            return extracted;
        }

        private static string ComposeUserMessageWithAttachment(string userText, string filePath, string extractedText)
        {
            var fileName = Path.GetFileName(filePath);

            var instruction = string.IsNullOrWhiteSpace(userText)
                ? "Analiza el archivo adjunto y responde en español."
                : userText;

            return $"{instruction}\n\n[ARCHIVO_ADJUNTO]\nNombre: {fileName}\nContenido extraído:\n{extractedText}\n[/ARCHIVO_ADJUNTO]";
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos compatibles (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|PDF (*.pdf)|*.pdf|Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title = "Seleccionar archivo para conversación"
            };

            if (dialog.ShowDialog() == true)
            {
                _attachedFilePath = dialog.FileName;
                txtAttachment.Text = $"Adjunto listo: {Path.GetFileName(_attachedFilePath)}";
                txtAttachment.Visibility = Visibility.Visible;
                btnClearAttachment.Visibility = Visibility.Visible;
            }
        }

        private void ClearAttachment_Click(object sender, RoutedEventArgs e)
        {
            ClearAttachment();
        }

        private void ClearAttachment()
        {
            _attachedFilePath = null;
            txtAttachment.Text = string.Empty;
            txtAttachment.Visibility = Visibility.Collapsed;
            btnClearAttachment.Visibility = Visibility.Collapsed;
        }

        private Task CheckAiStatusAsync()
        {
            var hasGoogleKey = !string.IsNullOrWhiteSpace(SecureSecretsService.GetGoogleApiKey());
            var hasGroqKey = !string.IsNullOrWhiteSpace(SecureSecretsService.GetGroqApiKey());
            var options = AiSettingsService.GetOptions();

            Dispatcher.Invoke(() =>
            {
                txtModelInfo.Text =
                    $"Google: {options.GoogleChatModel} | Visión: {options.GoogleVisionModel} | Groq: {options.GroqChatModel}";

                if (hasGoogleKey)
                {
                    elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(6, 148, 148));
                    txtAiStatus.Text = hasGroqKey
                        ? "IA cloud lista ✓"
                        : "Google listo (sin respaldo Groq)";
                }
                else
                {
                    elAiStatus.Fill = new SolidColorBrush(Color.FromRgb(200, 80, 80));
                    txtAiStatus.Text = "Falta Google API key";
                }
            });

            return Task.CompletedTask;
        }
    }
}
