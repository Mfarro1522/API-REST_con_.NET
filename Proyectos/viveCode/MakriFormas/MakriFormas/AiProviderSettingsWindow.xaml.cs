using System;
using System.Linq;
using System.Windows;
using MakriFormas.Services;

namespace MakriFormas
{
    public partial class AiProviderSettingsWindow : Window
    {
        public AiProviderSettingsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            cmbGoogleChatModel.ItemsSource = AiSettingsService.GetGoogleCheapModels().ToList();
            cmbGoogleVisionModel.ItemsSource = AiSettingsService.GetGoogleCheapModels().ToList();
            cmbGroqChatModel.ItemsSource = AiSettingsService.GetGroqCheapModels().ToList();

            var options = AiSettingsService.GetOptions();
            cmbGoogleChatModel.SelectedItem = options.GoogleChatModel;
            cmbGoogleVisionModel.SelectedItem = options.GoogleVisionModel;
            cmbGroqChatModel.SelectedItem = options.GroqChatModel;
            chkEnableGroqFallback.IsChecked = options.EnableGroqFallback;

            var googleKey = SecureSecretsService.GetGoogleApiKey();
            var groqKey = SecureSecretsService.GetGroqApiKey();
            pwdGoogleApiKey.Password = googleKey;
            pwdGroqApiKey.Password = groqKey;

            txtGoogleStatus.Text = string.IsNullOrWhiteSpace(googleKey)
                ? "Clave Google pendiente."
                : "Clave Google configurada.";

            txtGroqStatus.Text = string.IsNullOrWhiteSpace(groqKey)
                ? "Clave Groq pendiente (recomendado para fallback)."
                : "Clave Groq configurada.";

            txtSaveStatus.Text = "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var googleChat = cmbGoogleChatModel.SelectedItem?.ToString() ?? string.Empty;
                var googleVision = cmbGoogleVisionModel.SelectedItem?.ToString() ?? string.Empty;
                var groqChat = cmbGroqChatModel.SelectedItem?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(googleChat) ||
                    string.IsNullOrWhiteSpace(googleVision) ||
                    string.IsNullOrWhiteSpace(groqChat))
                {
                    txtSaveStatus.Text = "Selecciona un modelo para cada bloque.";
                    return;
                }

                AiSettingsService.SaveOptions(new AiOptions
                {
                    GoogleChatModel = googleChat,
                    GoogleVisionModel = googleVision,
                    GroqChatModel = groqChat,
                    EnableGroqFallback = chkEnableGroqFallback.IsChecked == true
                });

                SecureSecretsService.SetGoogleApiKey(pwdGoogleApiKey.Password?.Trim() ?? string.Empty);
                SecureSecretsService.SetGroqApiKey(pwdGroqApiKey.Password?.Trim() ?? string.Empty);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtSaveStatus.Text = $"No se pudo guardar: {ex.Message}";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
