using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    public sealed class GroqAiProvider : IAiProvider
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://api.groq.com"),
            Timeout = TimeSpan.FromSeconds(120)
        };

        public string ProviderName => "Groq";

        public async Task<AiProviderResult> GenerateTextAsync(
            string systemPrompt,
            string userMessage,
            string model,
            string apiKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return AiProviderResult.Fail("Groq API key no configurada.");

            try
            {
                var body = new
                {
                    model,
                    temperature = 0.1,
                    stream = false,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "/openai/v1/chat/completions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var response = await Http.SendAsync(request, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AiProviderResult.Fail(ParseGroqError(raw), (int)response.StatusCode);

                var content = ParseGroqText(raw);
                if (string.IsNullOrWhiteSpace(content))
                    return AiProviderResult.Fail("Groq devolvió respuesta vacía.", (int)response.StatusCode);

                return AiProviderResult.Ok(content);
            }
            catch (TaskCanceledException)
            {
                return AiProviderResult.Fail("Tiempo de espera agotado con Groq.");
            }
            catch (Exception ex)
            {
                return AiProviderResult.Fail($"Error en Groq: {ex.Message}");
            }
        }

        public Task<AiProviderResult> GenerateFromImageAsync(
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string mimeType,
            string model,
            string apiKey,
            CancellationToken ct = default)
        {
            _ = systemPrompt;
            _ = userPrompt;
            _ = imageBytes;
            _ = mimeType;
            _ = model;
            _ = apiKey;
            _ = ct;

            // En esta implementación priorizamos visión en Google para costos/calidad.
            return Task.FromResult(AiProviderResult.Fail("Groq no está habilitado para visión en esta configuración."));
        }

        private static string ParseGroqText(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (!root.TryGetProperty("choices", out var choices) ||
                    choices.ValueKind != JsonValueKind.Array ||
                    choices.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                var first = choices[0];
                if (!first.TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("content", out var content) ||
                    content.ValueKind != JsonValueKind.String)
                {
                    return string.Empty;
                }

                return content.GetString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ParseGroqError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Error desconocido en Groq.";

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err))
                {
                    if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        return msg.GetString() ?? "Error desconocido en Groq.";
                }
            }
            catch
            {
                // ignore
            }

            return raw.Length > 300 ? raw[..300] : raw;
        }
    }
}
