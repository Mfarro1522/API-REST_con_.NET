using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        private CancellationTokenSource? _cts;
        private string _currentModel = OllamaService.ChatPrimaryModel;

        public AgentChatWindow()
        {
            InitializeComponent();
            icMessages.ItemsSource = Messages;

            // Mensaje de bienvenida
            AddAgentMessage("¡Hola! Soy el asistente de MakriFormas 🤖\n" +
                            "Puedo ayudarte a gestionar productos, crear proformas y más.\n" +
                            "¿En qué te ayudo hoy?");

            _ = CheckOllamaStatusAsync();
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
            if (string.IsNullOrWhiteSpace(text)) return;

            txtInput.Text = string.Empty;
            btnSend.IsEnabled = false;

            // Burbuja del usuario
            Messages.Add(new ChatMessage { IsUser = true, Content = text });
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
                var accum = new System.Text.StringBuilder();

                var response = await AgentDispatcher.ProcessStreamAsync(
                    text,
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
                    model: _currentModel,
                    ct: _cts.Token);

                // Mostrar el mensaje limpio del agente (sin JSON crudo)
                if (!string.IsNullOrWhiteSpace(response.Message))
                {
                    agentMsg.Content = response.Message;
                }

                // ── Acción: crear proforma ────────────────────────────────────
                if (response.Data is ProformaPreFillData prefill)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var proformaWin = new NewProformaWindow();

                        // Heredar owner si es posible
                        if (Owner != null)
                            proformaWin.Owner = Owner;

                        proformaWin.Show();
                        proformaWin.PreFill(prefill);

                        // Cuando se guarde la proforma, notificar el dashboard
                        proformaWin.Closed += (_, _) => DbChanged?.Invoke(this, EventArgs.Empty);
                    });
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
                _cts?.Dispose();
                _cts = null;
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

        private async Task CheckOllamaStatusAsync()
        {
            // Primer chequeo inmediato
            var ok = await OllamaLauncher.IsPingOkAsync();

            if (!ok)
            {
                // Ollama todavía arrancando — mostrar "Iniciando..."
                Dispatcher.Invoke(() =>
                {
                    elOllamaStatus.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // ámbar
                    txtOllamaStatus.Text = "Iniciando Ollama...";
                    txtModelInfo.Text = $"Modelo: {OllamaService.ChatPrimaryModel} | Respaldo: {OllamaService.ChatFallbackModel}";
                });

                // Esperar hasta 30 s reintentando cada 2 s
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(2000);
                    ok = await OllamaLauncher.IsPingOkAsync();
                    if (ok) break;
                }
            }

            Dispatcher.Invoke(() =>
            {
                if (ok)
                {
                    elOllamaStatus.Fill = new SolidColorBrush(Color.FromRgb(6, 148, 148));
                    txtOllamaStatus.Text = "Ollama activo ✓";
                    txtModelInfo.Text = $"Modelo: {OllamaService.ChatPrimaryModel} | Respaldo: {OllamaService.ChatFallbackModel}";
                }
                else
                {
                    elOllamaStatus.Fill = new SolidColorBrush(Color.FromRgb(200, 80, 80));
                    txtOllamaStatus.Text = "Ollama no disponible";
                    txtModelInfo.Text = "El asistente no puede responder";
                }
            });
        }
    }
}
