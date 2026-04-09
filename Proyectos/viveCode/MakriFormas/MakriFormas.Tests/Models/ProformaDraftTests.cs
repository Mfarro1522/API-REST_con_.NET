using MakriFormas.Models;

namespace MakriFormas.Tests.Models;

/// <summary>
/// Tests del modelo ProformaDraft: propiedades calculadas y null-safety.
/// </summary>
public class ProformaDraftTests
{
    // ── Total ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Total_Empty_IsZero()
    {
        var draft = new ProformaDraft();
        Assert.Equal(0.0, draft.Total);
    }

    [Fact]
    public void Total_NullItems_DoesNotThrow_AndIsZero()
    {
        var draft = new ProformaDraft { Items = null! };
        var total = draft.Total; // ← antes lanzaría NullReferenceException
        Assert.Equal(0.0, total);
    }

    [Fact]
    public void Total_SumsAllItemTotals()
    {
        var draft = new ProformaDraft
        {
            Items = new List<ProformaItem>
            {
                new() { Unidad = "unidad", Cantidad = 2, UnitPrice = 10 }, // 20
                new() { Unidad = "m2", Ancho = 1, Alto = 2, Cantidad = 3, UnitPrice = 5 }, // 30
            }
        };

        Assert.Equal(50.0, draft.Total, precision: 6);
    }

    // ── ItemCount ─────────────────────────────────────────────────────────────

    [Fact]
    public void ItemCount_NullItems_DoesNotThrow_AndIsZero()
    {
        var draft = new ProformaDraft { Items = null! };
        Assert.Equal(0, draft.ItemCount);
    }

    [Fact]
    public void ItemCount_ExcludesItemsWithZeroCantidad()
    {
        var draft = new ProformaDraft
        {
            Items = new List<ProformaItem>
            {
                new() { Description = "Válido", Cantidad = 1, UnitPrice = 10 },
                new() { Description = "Sin cantidad", Cantidad = 0, UnitPrice = 10 },
            }
        };

        Assert.Equal(1, draft.ItemCount);
    }

    [Fact]
    public void ItemCount_ExcludesItemsWithEmptyDescription()
    {
        var draft = new ProformaDraft
        {
            Items = new List<ProformaItem>
            {
                new() { Description = "Válido",  Cantidad = 1, UnitPrice = 5 },
                new() { Description = "",        Cantidad = 2, UnitPrice = 5 },
                new() { Description = "   ",     Cantidad = 1, UnitPrice = 5 },
            }
        };

        Assert.Equal(1, draft.ItemCount);
    }

    [Fact]
    public void ItemCount_OnlyCountsValidItems()
    {
        var draft = new ProformaDraft
        {
            Items = new List<ProformaItem>
            {
                new() { Description = "A", Cantidad = 1, UnitPrice = 1 },
                new() { Description = "B", Cantidad = 2, UnitPrice = 1 },
                new() { Description = "C", Cantidad = 0, UnitPrice = 1 }, // inválido
                new() { Description = "",  Cantidad = 1, UnitPrice = 1 }, // inválido
            }
        };

        Assert.Equal(2, draft.ItemCount);
    }

    // ── Valores por defecto ───────────────────────────────────────────────────

    [Fact]
    public void ProformaDraft_DefaultValues_AreCorrect()
    {
        var draft = new ProformaDraft();

        Assert.NotNull(draft.Items);
        Assert.Empty(draft.Items);
        Assert.Equal(string.Empty, draft.ClientName);
        Assert.Equal(string.Empty, draft.Code);
        Assert.Null(draft.Id);
        Assert.True(draft.IssueDate <= DateTime.Now);
    }
}
