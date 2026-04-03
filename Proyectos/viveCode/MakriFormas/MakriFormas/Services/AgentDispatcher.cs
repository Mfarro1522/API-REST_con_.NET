using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MakriFormas.Models;

namespace MakriFormas.Services
{
    // ── DTOs para pre-llenado de proforma ────────────────────────────────────

    public class ProformaItemRequest
    {
        public string Description { get; set; } = string.Empty;
        public string Unit        { get; set; } = "unidad";
        public double Ancho       { get; set; }
        public double Alto        { get; set; }
        public double Longitud    { get; set; }
        public double Cantidad    { get; set; } = 1;
        public double Precio      { get; set; }
    }

    public class ProformaPreFillData
    {
        public string ClientName              { get; set; } = string.Empty;
        public string Ruc                     { get; set; } = string.Empty;
        public List<ProformaItemRequest> Items { get; set; } = new();
    }


    /// <summary>
    /// Resultado que devuelve el agente para cada turno de conversación.
    /// </summary>
    public record AgentResponse(string Message, bool DbChanged, object? Data = null);

    /// <summary>
    /// Parsea el JSON producido por el modelo y ejecuta la acción sobre la DB.
    /// Maneja JSON malformado con un reintento automático.
    /// </summary>
    public static class AgentDispatcher
    {
        // ── Prompt de corrección para cuando el modelo devuelve JSON inválido ──

        private const string CorrectionPrompt =
            "Tu última respuesta no era JSON válido. " +
            "Devuelve SOLO el JSON con el formato exacto: " +
            "{\"action\":\"chat\",\"params\":{},\"message\":\"tu respuesta aquí\"}. " +
            "Sin texto adicional fuera del JSON.";

        // ── Entry point principal ─────────────────────────────────────────────

        /// <summary>
        /// Procesa un mensaje del usuario. Si el JSON del modelo falla, reintenta una vez.
        /// </summary>
        public static async Task<AgentResponse> ProcessAsync(
            string userMessage,
            string model = "",
            CancellationToken ct = default)
        {
            var systemPrompt = OllamaService.LoadSystemPromptWithUnits();

            var candidates = OllamaService.GetChatModelCandidates(model);
            var errors = new List<string>();

            foreach (var candidate in candidates)
            {
                var rawResponse = await OllamaService.Instance.ChatOnceAsync(systemPrompt, userMessage, candidate, ct);
                if (IsQuotaOrModelFailure(rawResponse, out var reason))
                {
                    errors.Add($"{candidate}: {reason}");
                    continue;
                }

                if (TryParseAndDispatch(rawResponse, out var result))
                    return result!;

                // ── Reintento con prompt de corrección ────────────────────
                var correctionMessage = $"Tu respuesta anterior fue: {rawResponse}\n{CorrectionPrompt}";
                var retryResponse = await OllamaService.Instance.ChatOnceAsync(systemPrompt, correctionMessage, candidate, ct);

                if (IsQuotaOrModelFailure(retryResponse, out reason))
                {
                    errors.Add($"{candidate}: {reason}");
                    continue;
                }

                if (TryParseAndDispatch(retryResponse, out result))
                    return result!;
            }

            return new AgentResponse(
                errors.Count > 0
                    ? $"No pude responder con los modelos configurados. Detalle: {string.Join(" | ", errors)}"
                    : "No pude interpretar la respuesta de los modelos configurados.",
                DbChanged: false);
        }

        // ── Streaming: acumula tokens y despacha al terminar ─────────────────

        /// <summary>
        /// Versión streaming: llama al callback con cada token y al final ejecuta la acción.
        /// </summary>
        public static async Task<AgentResponse> ProcessStreamAsync(
            string userMessage,
            Action<string> onToken,
            string model = "",
            CancellationToken ct = default)
        {
            var systemPrompt = OllamaService.LoadSystemPromptWithUnits();

            var candidates = OllamaService.GetChatModelCandidates(model);
            var errors = new List<string>();

            foreach (var candidate in candidates)
            {
                var fullResponse = new System.Text.StringBuilder();

                await foreach (var token in OllamaService.Instance.ChatStreamAsync(systemPrompt, userMessage, candidate, ct))
                {
                    fullResponse.Append(token);
                }

                var raw = fullResponse.ToString();

                if (IsQuotaOrModelFailure(raw, out var reason))
                {
                    errors.Add($"{candidate}: {reason}");
                    continue;
                }

                onToken(raw);

                if (TryParseAndDispatch(raw, out var result))
                    return result!;

                // Reintento sin streaming en el mismo modelo
                var correctionMessage = $"Tu respuesta anterior fue: {raw}\n{CorrectionPrompt}";
                var retryResponse = await OllamaService.Instance.ChatOnceAsync(systemPrompt, correctionMessage, candidate, ct);

                if (IsQuotaOrModelFailure(retryResponse, out reason))
                {
                    errors.Add($"{candidate}: {reason}");
                    continue;
                }

                if (TryParseAndDispatch(retryResponse, out result))
                    return result!;
            }

            return new AgentResponse(
                errors.Count > 0
                    ? $"No pude responder con los modelos configurados. Detalle: {string.Join(" | ", errors)}"
                    : "No pude interpretar la respuesta de los modelos configurados.",
                DbChanged: false);
        }

        private static bool IsQuotaOrModelFailure(string raw, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                reason = "respuesta vacía";
                return true;
            }

            var txt = raw.Trim();

            if (txt.Contains("[MODEL_ERROR]", StringComparison.OrdinalIgnoreCase))
            {
                reason = txt;
                return true;
            }

            var quotaKeywords = new[]
            {
                "quota",
                "rate limit",
                "too many requests",
                "límite",
                "limite",
                "exceeded",
                "credit",
                "insufficient"
            };

            foreach (var keyword in quotaKeywords)
            {
                if (txt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    reason = txt;
                    return true;
                }
            }

            return false;
        }

        // ── Parser + Dispatcher ───────────────────────────────────────────────

        private static bool TryParseAndDispatch(string raw, out AgentResponse? result)
        {
            result = null;

            // El modelo a veces envuelve el JSON en bloques ```json ... ```
            var clean = CleanJsonFences(raw.Trim());

            try
            {
                using var doc = JsonDocument.Parse(clean);
                var root = doc.RootElement;

                var action  = root.TryGetProperty("action",  out var a) ? a.GetString() ?? "chat" : "chat";
                var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                var @params = root.TryGetProperty("params",  out var p) ? p : default;

                result = Dispatch(action, message, @params);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static AgentResponse Dispatch(string action, string message, JsonElement @params)
        {
            switch (action)
            {
                case "add_product":
                    return DispatchAddProduct(@params, message);

                case "update_price":
                    return DispatchUpdatePrice(@params, message);

                case "delete_proforma":
                    return DispatchDeleteProforma(@params, message);

                case "list_products":
                case "search_products":
                    return DispatchListProducts(@params, message);

                case "create_proforma":
                    var prefill = ParseProformaRequest(@params);
                    return new AgentResponse(message, DbChanged: false, Data: prefill);

                case "chat":
                default:
                    return new AgentResponse(message, DbChanged: false);
            }
        }

        // ── Acciones concretas ────────────────────────────────────────────────

        private static AgentResponse DispatchAddProduct(JsonElement @params, string message)
        {
            try
            {
                var product = new Product
                {
                    Sku           = GetString(@params, "sku", "GEN-001"),
                    Name          = GetString(@params, "name", "Producto sin nombre"),
                    Category      = GetString(@params, "category", "Materiales"),
                    Unit          = GetString(@params, "unit", "unidad"),
                    UnitPrice     = GetDouble(@params, "unitPrice"),
                    StockQuantity = GetDouble(@params, "stock")
                };

                DatabaseService.AddProduct(product);
                return new AgentResponse(message, DbChanged: true, Data: product);
            }
            catch (Exception ex)
            {
                return new AgentResponse($"Error al agregar producto: {ex.Message}", DbChanged: false);
            }
        }

        private static AgentResponse DispatchUpdatePrice(JsonElement @params, string message)
        {
            try
            {
                var id    = (int)GetDouble(@params, "productId");
                var price = GetDouble(@params, "newPrice");
                DatabaseService.UpdateProductPrice(id, price);
                return new AgentResponse(message, DbChanged: true);
            }
            catch (Exception ex)
            {
                return new AgentResponse($"Error al actualizar precio: {ex.Message}", DbChanged: false);
            }
        }

        private static AgentResponse DispatchDeleteProforma(JsonElement @params, string message)
        {
            try
            {
                var id = (int)GetDouble(@params, "proformaId");
                DatabaseService.DeleteProforma(id);
                return new AgentResponse(message, DbChanged: true);
            }
            catch (Exception ex)
            {
                return new AgentResponse($"Error al eliminar proforma: {ex.Message}", DbChanged: false);
            }
        }

        private static AgentResponse DispatchListProducts(JsonElement @params, string message)
        {
            try
            {
                var products = DatabaseService.GetProducts();
                var query    = GetString(@params, "query", string.Empty);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    products = products.FindAll(p =>
                        p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        p.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Contains(query, StringComparison.OrdinalIgnoreCase));
                }

                var summary = products.Count == 0
                    ? "No se encontraron productos."
                    : $"Encontré {products.Count} producto(s).";

                return new AgentResponse(
                    string.IsNullOrWhiteSpace(message) ? summary : message,
                    DbChanged: false,
                    Data: products);
            }
            catch (Exception ex)
            {
                return new AgentResponse($"Error al listar productos: {ex.Message}", DbChanged: false);
            }
        }

        // ── Parse create_proforma params ──────────────────────────────────────

        private static ProformaPreFillData ParseProformaRequest(JsonElement @params)
        {
            var data = new ProformaPreFillData
            {
                ClientName = GetString(@params, "clientName", string.Empty),
                Ruc        = GetString(@params, "ruc", string.Empty)
            };

            if (@params.TryGetProperty("items", out var itemsEl) &&
                itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    data.Items.Add(new ProformaItemRequest
                    {
                        Description = GetString(item, "description", string.Empty),
                        Unit        = GetString(item, "unit", "unidad"),
                        Ancho       = GetDouble(item, "ancho"),
                        Alto        = GetDouble(item, "alto"),
                        Longitud    = GetDouble(item, "longitud"),
                        Cantidad    = Math.Max(1, GetDouble(item, "cantidad")),
                        Precio      = GetDouble(item, "precio")
                    });
                }
            }

            return data;
        }

        // ── Utils ─────────────────────────────────────────────────────────────

        private static string GetString(JsonElement el, string prop, string fallback)
        {
            return el.TryGetProperty(prop, out var v) ? v.GetString() ?? fallback : fallback;
        }

        private static double GetDouble(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return 0;
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDouble(),
                JsonValueKind.String => double.TryParse(v.GetString(), out var d) ? d : 0,
                _ => 0
            };
        }

        private static string CleanJsonFences(string raw)
        {
            if (raw.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                raw = raw["```json".Length..];
            if (raw.StartsWith("```"))
                raw = raw[3..];
            if (raw.EndsWith("```"))
                raw = raw[..^3];
            return raw.Trim();
        }
    }
}
