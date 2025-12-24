using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace PortfolioWatch.Models
{
    public enum TaxStatusType
    {
        Unspecified,
        NonTaxableRoth,
        TaxablePreTaxIRA,
        TaxableCapitalGains,
        Custom // Added for dynamic categories
    }

    public class TaxCategory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#888888";
        public TaxStatusType Type { get; set; } = TaxStatusType.Custom;
    }

    public class TaxAllocation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CategoryId { get; set; } // Link to TaxCategory

        // Deprecated but kept for migration/compatibility
        public TaxStatusType Type { get; set; } = TaxStatusType.Custom;
        
        private string _name = string.Empty;
        public string Name 
        { 
            get
            {
                // Fallback for legacy data
                if (string.IsNullOrEmpty(_name))
                {
                    return Type switch
                    {
                        TaxStatusType.Unspecified => "Unspecified",
                        TaxStatusType.NonTaxableRoth => "Non-Taxable Roth",
                        TaxStatusType.TaxablePreTaxIRA => "Taxable Pre-Tax IRA",
                        TaxStatusType.TaxableCapitalGains => "Taxable Capital Gains",
                        _ => "Custom Category"
                    };
                }
                return _name;
            }
            set => _name = value;
        }

        public double Percentage { get; set; }

        [JsonIgnore]
        public double Value { get; set; }

        private string _colorHex = string.Empty;
        public string ColorHex
        {
            get
            {
                if (string.IsNullOrEmpty(_colorHex))
                {
                    return Type switch
                    {
                        TaxStatusType.Unspecified => "#808080", // Gray
                        TaxStatusType.NonTaxableRoth => "#228833", // Green
                        TaxStatusType.TaxablePreTaxIRA => "#EE6677", // Red
                        TaxStatusType.TaxableCapitalGains => "#0077BB", // Blue
                        _ => "#888888" // Default gray
                    };
                }
                return _colorHex;
            }
            set => _colorHex = value;
        }

        [JsonIgnore]
        public Color Color 
        {
            get
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(ColorHex);
                }
                catch
                {
                    return Colors.Gray;
                }
            }
        }
        
        [JsonIgnore]
        public Brush Brush => new SolidColorBrush(Color);

        public TaxAllocation Clone()
        {
            return new TaxAllocation
            {
                Id = Guid.NewGuid(), // New ID for clone
                CategoryId = this.CategoryId,
                Type = this.Type,
                Name = this.Name,
                Percentage = this.Percentage,
                ColorHex = this.ColorHex
            };
        }
    }
}
