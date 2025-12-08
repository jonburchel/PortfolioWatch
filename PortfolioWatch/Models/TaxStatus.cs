using System.Text.Json.Serialization;
using System.Windows.Media;

namespace PortfolioWatch.Models
{
    public enum TaxStatusType
    {
        Unspecified,
        NonTaxableRoth,
        TaxableAfterTaxIRA,
        TaxableCapitalGains
    }

    public class TaxAllocation
    {
        public TaxStatusType Type { get; set; }
        public double Percentage { get; set; }

        [JsonIgnore]
        public string Name => Type switch
        {
            TaxStatusType.Unspecified => "Unspecified",
            TaxStatusType.NonTaxableRoth => "Non-Taxable Roth",
            TaxStatusType.TaxableAfterTaxIRA => "Taxable After-Tax IRA",
            TaxStatusType.TaxableCapitalGains => "Taxable Capital Gains",
            _ => "Unknown"
        };

        [JsonIgnore]
        public Color Color => Type switch
        {
            TaxStatusType.Unspecified => Colors.Gray,
            TaxStatusType.NonTaxableRoth => Color.FromRgb(0, 255, 0), // Vibrant Green
            TaxStatusType.TaxableAfterTaxIRA => Colors.Red,
            TaxStatusType.TaxableCapitalGains => Colors.Orange, // Yellow/Amber
            _ => Colors.Transparent
        };
        
        [JsonIgnore]
        public Brush Brush => new SolidColorBrush(Color);
    }
}
