using System.Threading;
using System.Threading.Tasks;

namespace MakriFormas.Services
{
    public interface IAiProvider
    {
        string ProviderName { get; }

        Task<AiProviderResult> GenerateTextAsync(
            string systemPrompt,
            string userMessage,
            string model,
            string apiKey,
            CancellationToken ct = default);

        Task<AiProviderResult> GenerateFromImageAsync(
            string systemPrompt,
            string userPrompt,
            byte[] imageBytes,
            string mimeType,
            string model,
            string apiKey,
            CancellationToken ct = default);
    }

    public sealed class AiProviderResult
    {
        public bool Success { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public int StatusCode { get; init; }

        public static AiProviderResult Ok(string content) => new()
        {
            Success = true,
            Content = content ?? string.Empty
        };

        public static AiProviderResult Fail(string error, int statusCode = 0) => new()
        {
            Success = false,
            Error = error ?? "Error desconocido",
            StatusCode = statusCode
        };
    }

    public sealed class AiRouteResult
    {
        public bool Success { get; init; }
        public bool UsedFallback { get; init; }
        public string ProviderUsed { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public string Notice { get; init; } = string.Empty;

        public static AiRouteResult Ok(string provider, string content, bool usedFallback = false, string notice = "") => new()
        {
            Success = true,
            ProviderUsed = provider,
            Content = content ?? string.Empty,
            UsedFallback = usedFallback,
            Notice = notice ?? string.Empty
        };

        public static AiRouteResult Fail(string error) => new()
        {
            Success = false,
            Error = error ?? "Error desconocido"
        };
    }
}
