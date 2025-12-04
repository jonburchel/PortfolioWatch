using CommunityToolkit.Mvvm.ComponentModel;

namespace PortfolioWatch.Models
{
    public partial class StockSearchResult : ObservableObject
    {
        [ObservableProperty]
        private string _symbol = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _name = string.Empty;

        [ObservableProperty]
        private double? _price;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsUp))]
        private double? _change;

        [ObservableProperty]
        private double? _changePercent;
        
        public bool IsUp => Change >= 0;
        public string DisplayText => $"{Symbol} - {Name}";
    }
}
