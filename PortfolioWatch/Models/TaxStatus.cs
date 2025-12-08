using System.Text.Json.Serialization;
using System.Windows.Media;

namespace PortfolioWatch.Models
{
    public enum TaxStatusType
    {
        Unspecified,
        NonTaxableRoth,
        TaxablePreTaxIRA,
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
            TaxStatusType.TaxablePreTaxIRA => "Taxable Pre-Tax IRA",
            TaxStatusType.TaxableCapitalGains => "Taxable Capital Gains",
            _ => "Unknown"
        };

        [JsonIgnore]
        public Color Color => Type switch
        {
            TaxStatusType.Unspecified => Colors.Gray,
            TaxStatusType.NonTaxableRoth => Color.FromRgb(34, 136, 51), // Accessible Green (#228833)
            TaxStatusType.TaxablePreTaxIRA => Color.FromRgb(238, 102, 119), // Accessible Soft Red (#EE6677)
            TaxStatusType.TaxableCapitalGains => Color.FromRgb(0, 119, 187), // Accessible Blue (#0077BB)
            _ => Colors.Transparent
        };
        
        [JsonIgnore]
        public Brush Brush => new SolidColorBrush(Color);
    }
}
