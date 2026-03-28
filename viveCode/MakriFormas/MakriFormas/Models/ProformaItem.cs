using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MakriFormas.Models
{
    public class ProformaItem : INotifyPropertyChanged
    {
        private string description = string.Empty;
        private double width;
        private double height;
        private int quantity;
        private double unitPrice;
        private bool isAreaBased;
        
        public string Description
        {
            get => description;
            set { description = value; OnPropertyChanged(); }
        }

        public bool IsAreaBased
        {
            get => isAreaBased;
            set { isAreaBased = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double Width
        {
            get => width;
            set { width = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double Height
        {
            get => height;
            set { height = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public int Quantity
        {
            get => quantity;
            set { quantity = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double UnitPrice
        {
            get => unitPrice;
            set { unitPrice = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double Total => IsAreaBased ? (Width * Height * Quantity * UnitPrice) : (Quantity * UnitPrice);

        private void CalculateTotal()
        {
            OnPropertyChanged(nameof(Total));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
