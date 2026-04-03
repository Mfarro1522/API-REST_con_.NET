using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MakriFormas.Services
{
    public sealed class AiOptions
    {
        public string GoogleChatModel { get; set; } = "gemini-2.0-flash";
        public string GoogleVisionModel { get; set; } = "gemini-2.0-flash";
        public string GroqChatModel { get; set; } = "llama-3.1-8b-instant";
        public bool EnableGroqFallback { get; set; } = true;
    }

    public static class AiSettingsService
    {
        private const string GoogleChatModelKey = "AI.GoogleChatModel";
        private const string GoogleVisionModelKey = "AI.GoogleVisionModel";
        private const string GroqChatModelKey = "AI.GroqChatModel";
        private const string EnableGroqFallbackKey = "AI.EnableGroqFallback";

        private static readonly IReadOnlyList<string> GoogleCheapModels = new[]
        {
            "gemini-2.0-flash",
            "gemini-2.0-flash-lite",
            "gemini-1.5-flash",
            "gemini-1.5-flash-latest"
        };

        private static readonly IReadOnlyList<string> GroqCheapModels = new[]
        {
            "llama-3.1-8b-instant",
            "llama-3.3-70b-versatile"
        };

        public static IReadOnlyList<string> GetGoogleCheapModels() => GoogleCheapModels;
        public static IReadOnlyList<string> GetGroqCheapModels() => GroqCheapModels;

        public static void EnsureDefaults()
        {
            SetIfMissing(GoogleChatModelKey, GoogleCheapModels[0]);
            SetIfMissing(GoogleVisionModelKey, GoogleCheapModels[0]);
            SetIfMissing(GroqChatModelKey, GroqCheapModels[0]);
            SetIfMissing(EnableGroqFallbackKey, "true");
        }

        public static AiOptions GetOptions()
        {
            EnsureDefaults();

            return new AiOptions
            {
                GoogleChatModel = EnsureAllowedModel(
                    DatabaseService.GetSetting(GoogleChatModelKey, GoogleCheapModels[0]),
                    GoogleCheapModels,
                    GoogleCheapModels[0]),

                GoogleVisionModel = EnsureAllowedModel(
                    DatabaseService.GetSetting(GoogleVisionModelKey, GoogleCheapModels[0]),
                    GoogleCheapModels,
                    GoogleCheapModels[0]),

                GroqChatModel = EnsureAllowedModel(
                    DatabaseService.GetSetting(GroqChatModelKey, GroqCheapModels[0]),
                    GroqCheapModels,
                    GroqCheapModels[0]),

                EnableGroqFallback = bool.TryParse(DatabaseService.GetSetting(EnableGroqFallbackKey, "true"), out var enabled)
                    ? enabled
                    : true
            };
        }

        public static void SaveOptions(AiOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            DatabaseService.SetSetting(
                GoogleChatModelKey,
                EnsureAllowedModel(options.GoogleChatModel, GoogleCheapModels, GoogleCheapModels[0]));

            DatabaseService.SetSetting(
                GoogleVisionModelKey,
                EnsureAllowedModel(options.GoogleVisionModel, GoogleCheapModels, GoogleCheapModels[0]));

            DatabaseService.SetSetting(
                GroqChatModelKey,
                EnsureAllowedModel(options.GroqChatModel, GroqCheapModels, GroqCheapModels[0]));

            DatabaseService.SetSetting(EnableGroqFallbackKey, options.EnableGroqFallback ? "true" : "false");
        }

        public static string LoadAgentPromptWithUnits()
        {
            var promptPath = ResolvePromptPath("agent_system.txt");
            var unitsPath = ResolveConfigPath("unidades_negocio.json");

            var prompt = File.Exists(promptPath)
                ? File.ReadAllText(promptPath)
                : "Eres el asistente de MakriFormas. Responde SIEMPRE con JSON: {\"action\":\"chat\",\"params\":{},\"message\":\"...\"}";

            if (File.Exists(unitsPath))
            {
                var unitsJson = File.ReadAllText(unitsPath);
                prompt = prompt.Replace("{UNIDADES_PLACEHOLDER}", BuildUnitsSummary(unitsJson));
            }
            else
            {
                prompt = prompt.Replace("{UNIDADES_PLACEHOLDER}", "unidad, m2, metro, kg, oz");
            }

            return prompt;
        }

        public static string LoadPdfExtractPrompt()
        {
            var path = ResolvePromptPath("pdf_extract_system.txt");
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string EnsureAllowedModel(string candidate, IReadOnlyList<string> allowed, string fallback)
        {
            return allowed.Contains(candidate, StringComparer.OrdinalIgnoreCase)
                ? allowed.First(m => m.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                : fallback;
        }

        private static void SetIfMissing(string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(DatabaseService.GetSetting(key)))
                DatabaseService.SetSetting(key, defaultValue);
        }

        private static string BuildUnitsSummary(string unitsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(unitsJson);
                var sb = new StringBuilder();
                foreach (var unit in doc.RootElement.GetProperty("unidades").EnumerateArray())
                {
                    var clave = unit.TryGetProperty("clave", out var c) ? c.GetString() : "?";
                    var calculo = unit.TryGetProperty("calculo", out var ca) ? ca.GetString() : "";
                    var ejemplo = unit.TryGetProperty("ejemplo", out var ej) ? ej.GetString() : "";
                    sb.AppendLine($"- {clave}: cálculo = {calculo}, ejemplo = {ejemplo}");
                }
                return sb.ToString();
            }
            catch
            {
                return "unidad, m2, metro, kg, oz";
            }
        }

        private static string ResolvePromptPath(string filename)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "prompts", filename),
                Path.Combine(Environment.CurrentDirectory, "prompts", filename),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts", filename))
            };

            foreach (var path in candidates)
                if (File.Exists(path)) return path;

            return candidates[0];
        }

        private static string ResolveConfigPath(string filename)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config", filename),
                Path.Combine(Environment.CurrentDirectory, "config", filename),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", filename))
            };

            foreach (var path in candidates)
                if (File.Exists(path)) return path;

            return candidates[0];
        }
    }
}
