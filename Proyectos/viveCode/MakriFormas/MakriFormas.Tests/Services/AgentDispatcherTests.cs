using MakriFormas.Services;

namespace MakriFormas.Tests.Services;

/// <summary>
/// Tests de AgentDispatcher.TryParseAndDispatch — lógica pura de parseo JSON,
/// sin dependencias de red ni BD.
/// </summary>
public class AgentDispatcherTests
{
    // ── Acción: chat (happy path) ─────────────────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_ChatAction_ReturnsTrue_WithMessage()
    {
        var json = """{"action":"chat","params":{},"message":"Hola, ¿en qué te ayudo?"}""";

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok);
        Assert.NotNull(result);
        Assert.Equal("Hola, ¿en qué te ayudo?", result!.Message);
        Assert.False(result.DbChanged);
        Assert.Null(result.Data);
    }

    // ── JSON envuelto en cercas markdown ─────────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_JsonWithMarkdownFences_ParsesCorrectly()
    {
        var json = "```json\n{\"action\":\"chat\",\"params\":{},\"message\":\"respuesta\"}\n```";

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok);
        Assert.Equal("respuesta", result!.Message);
    }

    [Fact]
    public void TryParseAndDispatch_JsonWithCodeFenceOnly_ParsesCorrectly()
    {
        var json = "```\n{\"action\":\"chat\",\"params\":{},\"message\":\"ok\"}\n```";

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok);
        Assert.Equal("ok", result!.Message);
    }

    // ── JSON malformado ───────────────────────────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_InvalidJson_ReturnsFalse()
    {
        var ok = AgentDispatcher.TryParseAndDispatch("esto no es json", out var result);

        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public void TryParseAndDispatch_EmptyString_ReturnsFalse()
    {
        var ok = AgentDispatcher.TryParseAndDispatch(string.Empty, out var result);

        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public void TryParseAndDispatch_PartialJson_ReturnsFalse()
    {
        var ok = AgentDispatcher.TryParseAndDispatch("{\"action\":\"chat\"", out var result);

        Assert.False(ok);
        Assert.Null(result);
    }

    // ── Acción: create_proforma ───────────────────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_CreateProforma_ReturnsPreFillData()
    {
        var json = """
        {
            "action": "create_proforma",
            "params": {
                "clientName": "Empresa SAC",
                "ruc": "20123456789",
                "items": [
                    {"description": "Vinil mate", "unit": "m2", "ancho": 1.2, "alto": 0.8, "cantidad": 3, "precio": 45.0}
                ]
            },
            "message": "Proforma creada"
        }
        """;

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok);
        Assert.NotNull(result);
        Assert.Equal("Proforma creada", result!.Message);
        Assert.False(result.DbChanged);

        var prefill = Assert.IsType<ProformaPreFillData>(result.Data);
        Assert.Equal("Empresa SAC", prefill.ClientName);
        Assert.Equal("20123456789", prefill.Ruc);
        Assert.Single(prefill.Items);
        Assert.Equal("Vinil mate", prefill.Items[0].Description);
        Assert.Equal("m2", prefill.Items[0].Unit);
        Assert.Equal(1.2, prefill.Items[0].Ancho, precision: 6);
        Assert.Equal(3.0, prefill.Items[0].Cantidad, precision: 6);
    }

    [Fact]
    public void TryParseAndDispatch_CreateProforma_EmptyItems_ReturnsEmptyList()
    {
        var json = """
        {
            "action": "create_proforma",
            "params": {"clientName": "Test", "ruc": "", "items": []},
            "message": "ok"
        }
        """;

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);
        var prefill = Assert.IsType<ProformaPreFillData>(result!.Data);

        Assert.True(ok);
        Assert.Empty(prefill.Items);
    }

    // ── Acción desconocida → fallback a chat ─────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_UnknownAction_FallsBackToChat()
    {
        var json = """{"action":"funcion_inventada","params":{},"message":"No reconozco esto"}""";

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok); // el parse fue exitoso
        Assert.False(result!.DbChanged);
        Assert.Equal("No reconozco esto", result.Message);
    }

    // ── Campo message ausente ─────────────────────────────────────────────────

    [Fact]
    public void TryParseAndDispatch_MissingMessageField_UsesEmptyString()
    {
        var json = """{"action":"chat","params":{}}""";

        var ok = AgentDispatcher.TryParseAndDispatch(json, out var result);

        Assert.True(ok);
        Assert.Equal(string.Empty, result!.Message);
    }
}
