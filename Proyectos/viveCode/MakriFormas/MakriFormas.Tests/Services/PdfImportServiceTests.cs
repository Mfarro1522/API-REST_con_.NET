using MakriFormas.Models;
using MakriFormas.Services;

namespace MakriFormas.Tests.Services;

/// <summary>
/// Tests de lógica pura de PdfImportService:
/// - Levenshtein (algoritmo de distancia de edición)
/// - FindClosestProduct (deduplicación fuzzy)
/// - ParseProductList (parseo de JSON de la IA)
/// Sin dependencias de red, archivos ni BD.
/// </summary>
public class PdfImportServiceTests
{
    // ── Levenshtein ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("",       "abc",  3)]   // cadena vacía → longitud del otro
    [InlineData("abc",    "",     3)]   // otro vacío   → longitud del primero
    [InlineData("abc",    "abc",  0)]   // idénticas    → 0
    [InlineData("kitten", "sitting", 3)]// caso clásico de Levenshtein
    [InlineData("vinil",  "vinyl",   1)]// una edición: delete 'i' en pos 1 → "vnil" + insert 'y' → no, algoritmo encuentra camino de 1
    [InlineData("banner", "baner",   1)]// una letra de diferencia
    [InlineData("lona",   "tona",    1)]// substitución
    public void Levenshtein_ReturnsCorrectDistance(string a, string b, int expected)
    {
        var dist = PdfImportService.Levenshtein(a, b);
        Assert.Equal(expected, dist);
    }

    [Fact]
    public void Levenshtein_IsSymmetric()
    {
        // Levenshtein(a, b) == Levenshtein(b, a)
        Assert.Equal(
            PdfImportService.Levenshtein("vinil", "banner"),
            PdfImportService.Levenshtein("banner", "vinil"));
    }

    // ── FindClosestProduct ────────────────────────────────────────────────────

    private static List<Product> SampleCatalog() =>
    [
        new() { Id = 1, Name = "Vinil mate",       Sku = "V001", Category = "Viniles", UnitPrice = 45 },
        new() { Id = 2, Name = "Lona banner 440g", Sku = "L001", Category = "Lonas",   UnitPrice = 30 },
        new() { Id = 3, Name = "Tinta ecosolvente", Sku = "T001", Category = "Tintas", UnitPrice = 200 },
    ];

    [Fact]
    public void FindClosestProduct_ExactMatch_ReturnsProduct()
    {
        var result = PdfImportService.FindClosestProduct("Vinil mate", SampleCatalog());
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public void FindClosestProduct_CaseInsensitive_ReturnsProduct()
    {
        var result = PdfImportService.FindClosestProduct("VINIL MATE", SampleCatalog());
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public void FindClosestProduct_FuzzyMatch_WithinThreshold_ReturnsProduct()
    {
        // "Vinil mat" → distancia 1 de "Vinil mate" → debe encontrar
        var result = PdfImportService.FindClosestProduct("Vinil mat", SampleCatalog());
        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public void FindClosestProduct_TooDistant_ReturnsNull()
    {
        // Nombre completamente diferente → distancia > 3 → no coincide
        var result = PdfImportService.FindClosestProduct("Aluminio pulido", SampleCatalog());
        Assert.Null(result);
    }

    [Fact]
    public void FindClosestProduct_EmptyName_ReturnsNull()
    {
        var result = PdfImportService.FindClosestProduct(string.Empty, SampleCatalog());
        Assert.Null(result);
    }

    [Fact]
    public void FindClosestProduct_EmptyCatalog_ReturnsNull()
    {
        var result = PdfImportService.FindClosestProduct("Vinil", new List<Product>());
        Assert.Null(result);
    }

    // ── ParseProductList ──────────────────────────────────────────────────────

    [Fact]
    public void ParseProductList_ValidJsonArray_ParsesAllProducts()
    {
        var json = """
        [
            {"nombre": "Vinil mate",   "precio": 45.0, "unidad": "m2"},
            {"nombre": "Lona 440g",    "precio": 30.0, "unidad": "m2"},
            {"nombre": "Tinta epson",  "precio": 200.0,"unidad": "kg"}
        ]
        """;

        var result = PdfImportService.ParseProductList(json);

        Assert.Equal(3, result.Count);
        Assert.Equal("Vinil mate", result[0].Nombre);
        Assert.Equal(45.0, result[0].Precio, precision: 6);
        Assert.Equal("m2", result[0].Unidad);
    }

    [Fact]
    public void ParseProductList_JsonWrappedInObject_ExtractsArray()
    {
        // El modelo IA a veces devuelve {"productos": [...]}
        var json = """{"productos": [{"nombre": "Vinil", "precio": 40.0, "unidad": "m2"}]}""";

        var result = PdfImportService.ParseProductList(json);

        Assert.Single(result);
        Assert.Equal("Vinil", result[0].Nombre);
    }

    [Fact]
    public void ParseProductList_JsonWithMarkdownFences_ParsesCorrectly()
    {
        var json = "```json\n[{\"nombre\": \"Lona\", \"precio\": 30.0, \"unidad\": \"m2\"}]\n```";

        var result = PdfImportService.ParseProductList(json);

        Assert.Single(result);
        Assert.Equal("Lona", result[0].Nombre);
    }

    [Fact]
    public void ParseProductList_MalformedJson_ReturnsEmptyList()
    {
        var result = PdfImportService.ParseProductList("esto no es json { }}");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseProductList_EmptyString_ReturnsEmptyList()
    {
        var result = PdfImportService.ParseProductList(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseProductList_ItemsWithEmptyName_AreFiltered()
    {
        var json = """
        [
            {"nombre": "",       "precio": 10.0, "unidad": "unidad"},
            {"nombre": "Válido", "precio": 20.0, "unidad": "unidad"}
        ]
        """;

        var result = PdfImportService.ParseProductList(json);

        Assert.Single(result);
        Assert.Equal("Válido", result[0].Nombre);
    }

    [Fact]
    public void ParseProductList_MissingUnidad_DefaultsToUnidad()
    {
        var json = """[{"nombre": "Producto", "precio": 50.0}]""";

        var result = PdfImportService.ParseProductList(json);

        Assert.Single(result);
        Assert.Equal("unidad", result[0].Unidad);
    }
}
