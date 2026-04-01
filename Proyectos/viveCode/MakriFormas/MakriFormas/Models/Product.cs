namespace MakriFormas.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double StockQuantity { get; set; }
        public string Unit { get; set; } = "und";
        public double UnitPrice { get; set; }

        public string StockDisplay => $"{StockQuantity:N2} {Unit}";
        public string UnitPriceDisplay => $"S/ {UnitPrice:N2}";
    }
}