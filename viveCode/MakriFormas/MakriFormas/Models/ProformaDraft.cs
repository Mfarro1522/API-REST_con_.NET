using System;
using System.Collections.Generic;
using System.Linq;

namespace MakriFormas.Models
{
    public class ProformaDraft
    {
        public int? Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public DateTime? DeliveryDate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DesignerObservations { get; set; } = string.Empty;
        public string InstallerObservations { get; set; } = string.Empty;
        public List<ProformaItem> Items { get; set; } = new();

        public int ItemCount => Items.Count(i => i.Quantity > 0 && !string.IsNullOrWhiteSpace(i.Description));
        public double Total => Items.Sum(i => i.Total);
    }
}
