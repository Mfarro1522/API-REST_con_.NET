using System.Collections.Generic;
using MakriFormas.Models;

namespace MakriFormas.Models
{
    /// <summary>Producto importado desde un PDF antes de guardarlo en la DB.</summary>
    public class ImportedProductDto
    {
        public string Nombre  { get; set; } = string.Empty;
        public double Precio  { get; set; }
        public string Unidad  { get; set; } = "unidad";
    }

    /// <summary>Resultado de la deduplicación: nuevos, duplicados exactos, precios cambiados.</summary>
    public record ImportResult(
        List<ImportedProductDto> New,
        List<ImportedProductDto> ExactDuplicates,
        List<(Product Existing, ImportedProductDto Incoming)> PriceChanged
    );
}
