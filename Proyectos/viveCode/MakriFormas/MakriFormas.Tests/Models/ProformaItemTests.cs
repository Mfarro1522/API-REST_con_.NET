using MakriFormas.Models;

namespace MakriFormas.Tests.Models;

/// <summary>
/// Tests del cálculo dinámico de Total en ProformaItem.
/// Cubre todos los tipos de unidad definidos en unidades_negocio.json.
/// </summary>
public class ProformaItemTests
{
    // ── Unidades por área (m², cm²) ───────────────────────────────────────────

    [Theory]
    [InlineData("m2",  2.0, 3.0, 4.0, 10.0,  240.0)]  // 2 * 3 * 4 * 10
    [InlineData("m2",  1.5, 2.0, 1.0, 50.0,  150.0)]  // 1.5 * 2 * 1 * 50
    [InlineData("cm2", 10.0, 5.0, 2.0, 1.0,  100.0)]  // 10 * 5 * 2 * 1
    public void Total_AreaBased_IsAncho_x_Alto_x_Cantidad_x_Precio(
        string unidad, double ancho, double alto, double cantidad, double precio, double expected)
    {
        var item = new ProformaItem
        {
            Unidad    = unidad,
            Ancho     = ancho,
            Alto      = alto,
            Cantidad  = cantidad,
            UnitPrice = precio
        };

        Assert.Equal(expected, item.Total, precision: 6);
    }

    // ── Unidades por longitud (metro, cm) ─────────────────────────────────────

    [Theory]
    [InlineData("metro", 5.0, 3.0, 20.0, 300.0)]   // 5 * 3 * 20
    [InlineData("cm",    100.0, 2.0, 1.5, 300.0)]  // 100 * 2 * 1.5
    public void Total_LengthBased_IsLongitud_x_Cantidad_x_Precio(
        string unidad, double longitud, double cantidad, double precio, double expected)
    {
        var item = new ProformaItem
        {
            Unidad    = unidad,
            Longitud  = longitud,
            Cantidad  = cantidad,
            UnitPrice = precio
        };

        Assert.Equal(expected, item.Total, precision: 6);
    }

    // ── Unidades simples (unidad, kg, millares, cientos + cualquier otra) ─────

    [Theory]
    [InlineData("unidad",   5.0,  20.0,  100.0)]
    [InlineData("kg",       2.5,  80.0,  200.0)]
    [InlineData("millares", 3.0,  500.0, 1500.0)]
    [InlineData("cientos",  10.0, 12.0,  120.0)]
    [InlineData("und",      1.0,  99.9,  99.9)]   // unidad desconocida → fallback simple
    public void Total_SimpleUnit_IsCantidad_x_Precio(
        string unidad, double cantidad, double precio, double expected)
    {
        var item = new ProformaItem
        {
            Unidad    = unidad,
            Cantidad  = cantidad,
            UnitPrice = precio
        };

        Assert.Equal(expected, item.Total, precision: 6);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Total_ZeroPrice_IsZero()
    {
        var item = new ProformaItem { Unidad = "m2", Ancho = 5, Alto = 3, Cantidad = 2, UnitPrice = 0 };
        Assert.Equal(0.0, item.Total);
    }

    [Fact]
    public void Total_ZeroCantidad_IsZero()
    {
        var item = new ProformaItem { Unidad = "unidad", Cantidad = 0, UnitPrice = 100 };
        Assert.Equal(0.0, item.Total);
    }

    [Fact]
    public void Total_FiresPropertyChanged_WhenUnitPriceChanges()
    {
        var item = new ProformaItem { Unidad = "unidad", Cantidad = 1, UnitPrice = 10 };
        string? changedProp = null;
        item.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        item.UnitPrice = 50;

        Assert.Equal(nameof(ProformaItem.Total), changedProp);
    }

    [Fact]
    public void Total_FiresPropertyChanged_WhenUnidadChanges()
    {
        var item = new ProformaItem { Unidad = "unidad", Cantidad = 2, UnitPrice = 10 };
        bool totalFired = false;
        item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProformaItem.Total)) totalFired = true; };

        item.Unidad = "m2";

        Assert.True(totalFired);
    }

    [Fact]
    public void IsAreaBased_TrueFor_m2_And_cm2()
    {
        Assert.True(new ProformaItem { Unidad = "m2" }.IsAreaBased);
        Assert.True(new ProformaItem { Unidad = "cm2" }.IsAreaBased);
        Assert.False(new ProformaItem { Unidad = "unidad" }.IsAreaBased);
    }

    [Fact]
    public void IsMetro_TrueFor_metro_And_cm()
    {
        Assert.True(new ProformaItem { Unidad = "metro" }.IsMetro);
        Assert.True(new ProformaItem { Unidad = "cm" }.IsMetro);
        Assert.False(new ProformaItem { Unidad = "m2" }.IsMetro);
    }

    [Fact]
    public void Quantity_Alias_ReturnsInt_Of_Cantidad()
    {
        var item = new ProformaItem { Cantidad = 3.7 };
        Assert.Equal(3, item.Quantity); // trunca hacia abajo
    }
}
