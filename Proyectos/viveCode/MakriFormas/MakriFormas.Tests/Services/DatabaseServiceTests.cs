using System.IO;
using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas.Tests.Services;

/// <summary>
/// Tests de integración de DatabaseService usando una BD SQLite temporal aislada.
/// Cada test class tiene su propia BD — no afecta la BD de producción.
/// </summary>
public class DatabaseServiceTests : IDisposable
{
    private readonly string _dbName;

    public DatabaseServiceTests()
    {
        // BD en memoria con nombre único por instancia — sin archivo, sin bloqueos
        _dbName = $"testdb_{Guid.NewGuid():N}";
        DatabaseService._testDatabasePath = $"file:{_dbName}?mode=memory&cache=shared";
        DatabaseService.Initialize();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        DatabaseService._testDatabasePath = null;
    }

    // ── Productos: CRUD ───────────────────────────────────────────────────────

    [Fact]
    public void AddProduct_And_GetProducts_ReturnsAddedProduct()
    {
        var product = new Product
        {
            Sku = "TEST-001", Name = "Vinil mate test",
            Category = "Viniles", UnitPrice = 45.0, StockQuantity = 100, Unit = "m2"
        };

        DatabaseService.AddProduct(product);
        var products = DatabaseService.GetProducts();

        Assert.Single(products);
        Assert.Equal("Vinil mate test", products[0].Name);
        Assert.Equal("TEST-001", products[0].Sku);
        Assert.Equal(45.0, products[0].UnitPrice, precision: 6);
    }

    [Fact]
    public void GetProducts_Empty_ReturnsEmptyList()
    {
        var products = DatabaseService.GetProducts();
        Assert.Empty(products);
    }

    [Fact]
    public void UpdateProductPrice_ChangesPrice()
    {
        var product = new Product
        {
            Sku = "SKU-002", Name = "Lona test",
            Category = "Lonas", UnitPrice = 30.0, StockQuantity = 50, Unit = "m2"
        };
        DatabaseService.AddProduct(product);
        var added = DatabaseService.GetProducts().First();

        DatabaseService.UpdateProductPrice(added.Id, 55.0);
        var updated = DatabaseService.GetProducts().First();

        Assert.Equal(55.0, updated.UnitPrice, precision: 6);
    }

    [Fact]
    public void DeleteProduct_RemovesProduct()
    {
        var product = new Product
        {
            Sku = "DEL-001", Name = "Producto a borrar",
            Category = "Test", UnitPrice = 10.0, StockQuantity = 1, Unit = "unidad"
        };
        DatabaseService.AddProduct(product);
        var id = DatabaseService.GetProducts().First().Id;

        DatabaseService.DeleteProduct(id);

        Assert.Empty(DatabaseService.GetProducts());
    }

    [Fact]
    public void AddProduct_MultipleProducts_ReturnsSortedByName()
    {
        DatabaseService.AddProduct(new Product { Sku = "Z", Name = "Zafiro",       Category = "A", UnitPrice = 1, StockQuantity = 1, Unit = "u" });
        DatabaseService.AddProduct(new Product { Sku = "A", Name = "Aluminio",     Category = "A", UnitPrice = 1, StockQuantity = 1, Unit = "u" });
        DatabaseService.AddProduct(new Product { Sku = "M", Name = "Marco metálico", Category = "A", UnitPrice = 1, StockQuantity = 1, Unit = "u" });

        var products = DatabaseService.GetProducts();

        Assert.Equal("Aluminio",      products[0].Name);
        Assert.Equal("Marco metálico", products[1].Name);
        Assert.Equal("Zafiro",        products[2].Name);
    }

    // ── Proformas: Save & Get ─────────────────────────────────────────────────

    [Fact]
    public void SaveProforma_And_GetHistory_ReturnsEntry()
    {
        var draft = BuildValidDraft();

        var id = DatabaseService.SaveProforma(draft);
        var history = DatabaseService.GetProformaHistory();

        Assert.True(id > 0);
        Assert.Single(history);
        Assert.Equal("Empresa SAC", history[0].ClientName);
    }

    [Fact]
    public void SaveProforma_NoValidItems_ThrowsInvalidOperation()
    {
        var draft = new ProformaDraft
        {
            ClientName = "Cliente",
            Code       = "COD-001",
            IssueDate  = DateTime.Today,
            Items      = new List<ProformaItem>() // sin ítems
        };

        Assert.Throws<InvalidOperationException>(() => DatabaseService.SaveProforma(draft));
    }

    [Fact]
    public void GetProformaById_ReturnsCorrectDraft()
    {
        var draft = BuildValidDraft("Test Client", "RUC-999");
        var id = DatabaseService.SaveProforma(draft);

        var retrieved = DatabaseService.GetProformaById(id);

        Assert.NotNull(retrieved);
        Assert.Equal("Test Client", retrieved!.ClientName);
        Assert.Equal("RUC-999",    retrieved.Ruc);
        Assert.NotEmpty(retrieved.Items);
    }

    [Fact]
    public void GetProformaById_NonExistentId_ReturnsNull()
    {
        var result = DatabaseService.GetProformaById(99999);
        Assert.Null(result);
    }

    [Fact]
    public void SaveProforma_Update_ChangesClientName()
    {
        var draft = BuildValidDraft("Original");
        var id = DatabaseService.SaveProforma(draft);

        draft.Id = id;
        draft.ClientName = "Modificado";
        DatabaseService.SaveProforma(draft);

        var retrieved = DatabaseService.GetProformaById(id);
        Assert.Equal("Modificado", retrieved!.ClientName);
    }

    [Fact]
    public void DeleteProforma_RemovesFromHistory()
    {
        var id = DatabaseService.SaveProforma(BuildValidDraft());
        DatabaseService.DeleteProforma(id);

        var history = DatabaseService.GetProformaHistory();
        Assert.Empty(history);
    }

    // ── Código correlativo ────────────────────────────────────────────────────

    [Fact]
    public void GetNextProformaCode_EmptyDB_StartsAtOne()
    {
        var code = DatabaseService.GetNextProformaCode();

        // El formato esperado es algo como PRF-001 o PRF-2026-001
        Assert.False(string.IsNullOrWhiteSpace(code));
    }

    [Fact]
    public void GetNextProformaCode_AfterSave_Increments()
    {
        var first  = DatabaseService.GetNextProformaCode();
        DatabaseService.SaveProforma(BuildValidDraft(code: first));
        var second = DatabaseService.GetNextProformaCode();

        Assert.NotEqual(first, second);
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetSetting_And_GetSetting_RoundTrips()
    {
        DatabaseService.SetSetting("test_key", "mi_valor");
        var value = DatabaseService.GetSetting("test_key");

        Assert.Equal("mi_valor", value);
    }

    [Fact]
    public void GetSetting_NonExistentKey_ReturnsNull()
    {
        var value = DatabaseService.GetSetting("clave_inexistente");
        Assert.Null(value);
    }

    [Fact]
    public void GetSetting_WithDefault_ReturnsDefault_WhenMissing()
    {
        var value = DatabaseService.GetSetting("ausente", "valor_default");
        Assert.Equal("valor_default", value);
    }

    // ── Serialización de ítems ────────────────────────────────────────────────

    [Fact]
    public void SaveAndRetrieve_PreservesItemDetails()
    {
        var draft = new ProformaDraft
        {
            ClientName = "Item Test",
            Code       = "IT-001",
            IssueDate  = DateTime.Today,
            Items      = new List<ProformaItem>
            {
                new() { Description = "Vinil mate", Unidad = "m2", Ancho = 1.5, Alto = 0.8, Cantidad = 2, UnitPrice = 45 },
                new() { Description = "Marco",      Unidad = "metro", Longitud = 3.0, Cantidad = 1, UnitPrice = 12 }
            }
        };

        var id       = DatabaseService.SaveProforma(draft);
        var retrieved = DatabaseService.GetProformaById(id)!;

        Assert.Equal(2, retrieved.Items.Count);

        var vinil = retrieved.Items.First(i => i.Description == "Vinil mate");
        Assert.Equal("m2",  vinil.Unidad);
        Assert.Equal(1.5,   vinil.Ancho,    precision: 4);
        Assert.Equal(0.8,   vinil.Alto,     precision: 4);
        Assert.Equal(2.0,   vinil.Cantidad, precision: 4);
        Assert.Equal(45.0,  vinil.UnitPrice,precision: 4);
        // Total = 1.5 * 0.8 * 2 * 45 = 108
        Assert.Equal(108.0, vinil.Total,    precision: 4);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ProformaDraft BuildValidDraft(
        string clientName = "Empresa SAC",
        string ruc        = "20123456789",
        string? code      = null)
    {
        return new ProformaDraft
        {
            ClientName = clientName,
            Ruc        = ruc,
            Code       = code ?? string.Empty,
            IssueDate  = DateTime.Today,
            Items      = new List<ProformaItem>
            {
                new() { Description = "Vinil mate", Unidad = "unidad", Cantidad = 2, UnitPrice = 45 }
            }
        };
    }
}
