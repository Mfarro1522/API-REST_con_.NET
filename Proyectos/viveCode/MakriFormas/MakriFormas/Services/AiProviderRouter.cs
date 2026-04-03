using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    public sealed class AiProviderRouter
    {
        private static readonly AiProviderRouter _instance = new();
        public static AiProviderRouter Instance => _instance;

        private readonly IAiProvider _google = new GeminiAiProvider();
        private readonly IAiProvider _groq = new GroqAiProvider();

        private AiProviderRouter() { }

        public async Task<AiRouteResult> ChatTextAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            var options = AiSettingsService.GetOptions();
            var googleKey = SecureSecretsService.GetGoogleApiKey();

            var primary = await _google.GenerateTextAsync(systemPrompt, userMessage, options.GoogleChatModel, googleKey, ct);
            if (primary.Success)
            {
                return AiRouteResult.Ok(_google.ProviderName, primary.Content);
            }

            if (!options.EnableGroqFallback)
            {
                return AiRouteResult.Fail($"Google falló y fallback está desactivado: {primary.Error}");
            }

            var groqKey = SecureSecretsService.GetGroqApiKey();
            var backup = await _groq.GenerateTextAsync(systemPrompt, userMessage, options.GroqChatModel, groqKey, ct);
            if (backup.Success)
            {
                return AiRouteResult.Ok(
                    _groq.ProviderName,
                    backup.Content,
                    usedFallback: true,
                    notice: "Google no estuvo disponible. Se usó Groq como respaldo.");
            }

            var combined = new StringBuilder();
            combined.Append("Google: ").Append(primary.Error);
            combined.Append(" | Groq: ").Append(backup.Error);
            return AiRouteResult.Fail(combined.ToString());
        }

        public Task<AiRouteResult> StructureProductsAsync(string systemPrompt, string rawText, CancellationToken ct = default)
        {
            return ChatTextAsync(systemPrompt, rawText, ct);
        }

        public async Task<AiRouteResult> ExtractTextFromImageAsync(
            byte[] imageBytes,
            string mimeType,
            CancellationToken ct = default)
        {
            var options = AiSettingsService.GetOptions();
            var googleKey = SecureSecretsService.GetGoogleApiKey();

            const string systemPrompt =
                "Eres un asistente multimodal para lectura de pedidos y proformas. Extrae texto literal y ordenado sin inventar valores.";
            const string userPrompt =
                "Transcribe todo el texto visible de la imagen. Devuelve solo texto plano, sin JSON, sin markdown y sin comentarios.";

            var primary = await _google.GenerateFromImageAsync(
                systemPrompt,
                userPrompt,
                imageBytes,
                mimeType,
                options.GoogleVisionModel,
                googleKey,
                ct);

            if (primary.Success)
            {
                return AiRouteResult.Ok(_google.ProviderName, primary.Content);
            }

            return AiRouteResult.Fail($"No se pudo procesar imagen con Google: {primary.Error}");
        }

        public string BuildStatusSummary()
        {
            var options = AiSettingsService.GetOptions();
            return $"Principal: Google ({options.GoogleChatModel}) | Respaldo: Groq ({options.GroqChatModel})";
        }
    }
}
