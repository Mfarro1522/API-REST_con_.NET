using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    public sealed class GeminiAiProvider : IAiProvider
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        private static readonly string[] PreferredFlashModels =
        {
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-2.0-flash-lite",
            "gemini-1.5-flash",
            "gemini-1.5-flash-latest"
        };

        private static readonly SemaphoreSlim ModelCacheLock = new(1, 1);
        private static DateTime ModelCacheExpiresAtUtc = DateTime.MinValue;
        private static List<string> CachedGenerateContentModels = new();

        public string ProviderName => "Google Gemini";

        public async Task<AiProviderResult> GenerateTextAsync(
            string systemPrompt,
            string userMessage,
            string model,
            string apiKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return AiProviderResult.Fail("Google API key no configurada.");

            var resolvedModel = await ResolveModelAsync(apiKey, model, ct);
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{resolvedModel}:generateContent?key={apiKey}";

            var body = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 4096
                }
            };

            return await SendRequestAsync(endpoint, body, ct);
        }

        public async Task<AiProviderResult> GenerateFromImageAsync(
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string mimeType,
            string model,
            string apiKey,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return AiProviderResult.Fail("Google API key no configurada.");

            if (imageBytes == null || imageBytes.Length == 0)
                return AiProviderResult.Fail("Imagen vacía para análisis multimodal.");

            var resolvedModel = await ResolveModelAsync(apiKey, model, ct);
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{resolvedModel}:generateContent?key={apiKey}";
            var base64 = Convert.ToBase64String(imageBytes);

            var body = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = userPrompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 4096
                }
            };

            return await SendRequestAsync(endpoint, body, ct);
        }

        private static async Task<AiProviderResult> SendRequestAsync(string endpoint, object body, CancellationToken ct)
        {
            try
            {
                var payload = JsonSerializer.Serialize(body);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                using var response = await Http.SendAsync(request, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = ParseGeminiError(raw);
                    return AiProviderResult.Fail(error, (int)response.StatusCode);
                }

                var text = ParseGeminiText(raw);
                if (string.IsNullOrWhiteSpace(text))
                    return AiProviderResult.Fail("Google Gemini devolvió respuesta vacía.", (int)response.StatusCode);

                return AiProviderResult.Ok(text);
            }
            catch (TaskCanceledException)
            {
                return AiProviderResult.Fail("Tiempo de espera agotado con Google Gemini.");
            }
            catch (Exception ex)
            {
                return AiProviderResult.Fail($"Error en Google Gemini: {ex.Message}");
            }
        }

        private static async Task<string> ResolveModelAsync(string apiKey, string preferredModel, CancellationToken ct)
        {
            var normalizedPreferred = NormalizeModelName(preferredModel);

            var available = await GetGenerateContentModelsAsync(apiKey, ct);
            if (available.Count == 0)
                return string.IsNullOrWhiteSpace(normalizedPreferred) ? "gemini-2.0-flash" : normalizedPreferred;

            if (!string.IsNullOrWhiteSpace(normalizedPreferred) &&
                available.Any(m => m.Equals(normalizedPreferred, StringComparison.OrdinalIgnoreCase)))
            {
                return available.First(m => m.Equals(normalizedPreferred, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var preferred in PreferredFlashModels)
            {
                if (available.Any(m => m.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
                    return available.First(m => m.Equals(preferred, StringComparison.OrdinalIgnoreCase));
            }

            var anyFlash = available.FirstOrDefault(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(anyFlash) ? available[0] : anyFlash;
        }

        private static async Task<List<string>> GetGenerateContentModelsAsync(string apiKey, CancellationToken ct)
        {
            if (DateTime.UtcNow <= ModelCacheExpiresAtUtc && CachedGenerateContentModels.Count > 0)
                return CachedGenerateContentModels.ToList();

            await ModelCacheLock.WaitAsync(ct);
            try
            {
                if (DateTime.UtcNow <= ModelCacheExpiresAtUtc && CachedGenerateContentModels.Count > 0)
                    return CachedGenerateContentModels.ToList();

                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                using var response = await Http.SendAsync(request, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var parsed = ParseGenerateContentModels(raw);
                CachedGenerateContentModels = parsed;
                ModelCacheExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
                return parsed.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAI] Error al obtener lista de modelos: {ex.Message}");
                return new List<string>();
            }
            finally
            {
                ModelCacheLock.Release();
            }
        }

        private static List<string> ParseGenerateContentModels(string raw)
        {
            var result = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (var model in models.EnumerateArray())
                {
                    if (!model.TryGetProperty("supportedGenerationMethods", out var methods) ||
                        methods.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var supportsGenerateContent = methods.EnumerateArray()
                        .Any(m => m.ValueKind == JsonValueKind.String &&
                                  string.Equals(m.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase));

                    if (!supportsGenerateContent)
                        continue;

                    if (model.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        var normalized = NormalizeModelName(nameProp.GetString());
                        if (!string.IsNullOrWhiteSpace(normalized))
                            result.Add(normalized);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAI] Error al parsear lista de modelos: {ex.Message}");
                return new List<string>();
            }

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeModelName(string? model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return string.Empty;

            var clean = model.Trim();
            return clean.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? clean["models/".Length..]
                : clean;
        }

        private static string ParseGeminiText(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) ||
                    candidates.ValueKind != JsonValueKind.Array)
                    return string.Empty;

                var chunks = new List<string>();

                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (!candidate.TryGetProperty("content", out var content) ||
                        !content.TryGetProperty("parts", out var parts) ||
                        parts.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textPart) && textPart.ValueKind == JsonValueKind.String)
                        {
                            var value = textPart.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                chunks.Add(value);
                        }
                    }
                }

                return string.Join("\n", chunks).Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAI] Error al parsear respuesta de texto: {ex.Message}");
                return string.Empty;
            }
        }

        private static string ParseGeminiError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Error desconocido en Google Gemini.";

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        return msg.GetString() ?? "Error desconocido en Google Gemini.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiAI] Error al parsear mensaje de error: {ex.Message}");
                // ignore parse errors
            }

            return raw.Length > 300 ? raw[..300] : raw;
        }
    }
}
