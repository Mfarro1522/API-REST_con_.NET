using System;

namespace MakriFormas.Models
{
    public class ProformaHistoryEntry
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public double Total { get; set; }

        public string IssueDateDisplay => IssueDate.ToString("dd/MM/yyyy");
        public string DeliveryDateDisplay => DeliveryDate?.ToString("dd/MM/yyyy") ?? "-";
        public string TotalDisplay => $"S/ {Total:N2}";
    }
}
