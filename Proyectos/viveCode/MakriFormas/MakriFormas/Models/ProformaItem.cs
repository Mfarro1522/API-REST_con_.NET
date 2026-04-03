using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MakriFormas.Models
{
    public class ProformaItem : INotifyPropertyChanged
    {
        private string description = string.Empty;
        private string unidad = "unidad";
        private double ancho = 1;
        private double alto = 1;
        private double longitud = 1;
        private double cantidad = 1;
        private double unitPrice;

        public string Description
        {
            get => description;
            set { description = value; OnPropertyChanged(); }
        }

        /// <summary>Clave de unidad según unidades_negocio.json (ej: "m2", "metro", "unidad")</summary>
        public string Unidad
        {
            get => unidad;
            set { unidad = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAreaBased)); OnPropertyChanged(nameof(IsMetro)); OnPropertyChanged(nameof(IsSimpleUnit)); CalculateTotal(); }
        }

        public double Ancho
        {
            get => ancho;
            set { ancho = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double Alto
        {
            get => alto;
            set { alto = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public double Longitud
        {
            get => longitud;
            set { longitud = value; OnPropertyChanged(); CalculateTotal(); }
        }

        /// <summary>Cantidad / número de piezas (double para soportar fracciones como 2.5 m²)</summary>
        public double Cantidad
        {
            get => cantidad;
            set { cantidad = value; OnPropertyChanged(); OnPropertyChanged(nameof(Quantity)); CalculateTotal(); }
        }

        public double UnitPrice
        {
            get => unitPrice;
            set { unitPrice = value; OnPropertyChanged(); CalculateTotal(); }
        }

        // ── Propiedades de compatibilidad ────────────────────────────────────

        /// <summary>Alias de compatibilidad con el JSON guardado (Quantity = (int)Cantidad)</summary>
        public int Quantity
        {
            get => (int)cantidad;
            set { Cantidad = value; }
        }

        /// <summary>Alias de compatibilidad</summary>
        public bool IsAreaBased
        {
            get => unidad == "m2" || unidad == "cm2";
            set
            {
                if (value)
                    Unidad = "m2";
                else if (unidad == "m2" || unidad == "cm2")
                    Unidad = "unidad";
            }
        }

        public bool IsMetro => unidad == "metro" || unidad == "cm";
        public bool IsSimpleUnit => !IsAreaBased && !IsMetro;

        // Mantener Width/Height como alias para retrocompatibilidad del JSON viejo
        public double Width
        {
            get => ancho;
            set => Ancho = value;
        }

        public double Height
        {
            get => alto;
            set => Alto = value;
        }

        // ── Total dinámico ───────────────────────────────────────────────────

        public double Total => Unidad switch
        {
            "m2"    => Ancho * Alto * Cantidad * UnitPrice,
            "cm2"   => Ancho * Alto * Cantidad * UnitPrice,
            "metro" => Longitud * Cantidad * UnitPrice,
            "cm"    => Longitud * Cantidad * UnitPrice,
            _       => Cantidad * UnitPrice
        };

        private void CalculateTotal() => OnPropertyChanged(nameof(Total));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
