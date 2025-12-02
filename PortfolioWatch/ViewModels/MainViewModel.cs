using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioWatch.Models;
using PortfolioWatch.Services;

namespace PortfolioWatch.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly SettingsService _settingsService;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private ObservableCollection<Stock> _stocks = new ObservableCollection<Stock>();

        [ObservableProperty]
        private ObservableCollection<Stock> _indexes = new ObservableCollection<Stock>();

        [ObservableProperty]
        private ObservableCollection<StockSearchResult> _searchResults = new ObservableCollection<StockSearchResult>();

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _newSymbol = string.Empty;

        [ObservableProperty]
        private bool _isSearchPopupOpen;

        [ObservableProperty]
        private string _sortProperty = string.Empty;

        [ObservableProperty]
        private bool _isAscending = true;

        [ObservableProperty]
        private string _windowTitle = "Watchlist (DEBUG)";

        [ObservableProperty]
        private bool _isIndexesVisible = true;

        [ObservableProperty]
        private bool _isPortfolioMode = false;

        [ObservableProperty]
        private decimal _totalPortfolioValue;

        [ObservableProperty]
        private decimal _totalPortfolioChange;

        [ObservableProperty]
        private double _totalPortfolioChangePercent;

        [ObservableProperty]
        private bool _isPortfolioUp;

        [ObservableProperty]
        private bool _startWithWindows;

        partial void OnStartWithWindowsChanged(bool value)
        {
            if (!IsBusy)
            {
                _settingsService.SetStartup(value);
            }
        }

        partial void OnWindowTitleChanged(string value)
        {
            if (!IsBusy) SaveStocks();
        }

        partial void OnIsIndexesVisibleChanged(bool value)
        {
            if (!IsBusy) SaveStocks();
        }

        partial void OnIsPortfolioModeChanged(bool value)
        {
            if (!IsBusy) SaveStocks();
            CalculatePortfolioTotals();
        }

        async partial void OnNewSymbolChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsSearchPopupOpen = false;
                SearchResults.Clear();
            }
            else
            {
                var results = await _stockService.SearchStocksAsync(value);
                SearchResults.Clear();
                foreach (var result in results)
                {
                    SearchResults.Add(new StockSearchResult { Symbol = result.Symbol, Name = result.Name });
                }
                IsSearchPopupOpen = SearchResults.Count > 0;
            }
        }

        public string SymbolSortIcon => SortProperty == "Symbol" ? (IsAscending ? "▲" : "▼") : "";
        public string NameSortIcon => SortProperty == "Name" ? (IsAscending ? "▲" : "▼") : "";
        public string ChangeSortIcon => SortProperty == "Change" ? (IsAscending ? "▲" : "▼") : "";
        public string DayChangeValueSortIcon => SortProperty == "DayChangeValue" ? (IsAscending ? "▲" : "▼") : "";
        public string MarketValueSortIcon => SortProperty == "MarketValue" ? (IsAscending ? "▲" : "▼") : "";

        public MainViewModel()
        {
            _stockService = new StockService();
            _settingsService = new SettingsService();
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _timer.Tick += Timer_Tick;
            
            LoadData();
            _timer.Start();
        }

        private async void LoadData()
        {
            IsBusy = true;
            
            var settings = _settingsService.LoadSettings();
            
            // Restore sort settings
            SortProperty = settings.SortColumn;
            IsAscending = settings.SortAscending;
            WindowTitle = settings.WindowTitle;
            
            // Load Indexes
            var indexes = await _stockService.GetIndexesAsync();
            Indexes = new ObservableCollection<Stock>(indexes);

            if (!settings.IsFirstRun && settings.Stocks != null)
            {
                // Use saved stocks (even if empty)
                _stockService.SetStocks(settings.Stocks);
                Stocks = new ObservableCollection<Stock>(settings.Stocks);
            }
            else
            {
                // First run or invalid settings: Use default stocks from service
                var defaultStocks = await _stockService.GetStocksAsync();
                Stocks = new ObservableCollection<Stock>(defaultStocks);
                
                // Save defaults
                SaveStocks();
            }

            // Subscribe to property changes for existing stocks
            foreach (var stock in Stocks)
            {
                stock.PropertyChanged += Stock_PropertyChanged;
            }
            Stocks.CollectionChanged += Stocks_CollectionChanged;

            // Restore IsIndexesVisible after stocks are loaded to prevent overwriting with empty list
            IsIndexesVisible = settings.IsIndexesVisible;
            IsPortfolioMode = settings.IsPortfolioMode;
            StartWithWindows = settings.StartWithWindows;

            // Apply sort
            ApplySortInternal();
            
            // Initial fetch
            await _stockService.UpdatePricesAsync();
            
            CalculatePortfolioTotals();

            IsBusy = false;
        }

        private void Stocks_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Stock stock in e.NewItems)
                {
                    stock.PropertyChanged += Stock_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (Stock stock in e.OldItems)
                {
                    stock.PropertyChanged -= Stock_PropertyChanged;
                }
            }
            CalculatePortfolioTotals();
        }

        private void Stock_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Stock.Shares) || 
                e.PropertyName == nameof(Stock.Price) || 
                e.PropertyName == nameof(Stock.Change))
            {
                CalculatePortfolioTotals();
                if (e.PropertyName == nameof(Stock.Shares))
                {
                    SaveStocks();
                }
            }
        }

        private void SaveStocks()
        {
            var settings = _settingsService.CurrentSettings;
            settings.Stocks = Stocks.ToList();
            settings.SortColumn = SortProperty;
            settings.SortAscending = IsAscending;
            settings.WindowTitle = WindowTitle;
            settings.IsIndexesVisible = IsIndexesVisible;
            settings.IsPortfolioMode = IsPortfolioMode;
            settings.IsFirstRun = false;
            _settingsService.SaveSettings(settings);
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await _stockService.UpdatePricesAsync();
            CalculatePortfolioTotals();
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Refresh()
        {
            await _stockService.UpdatePricesAsync();
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task AddStock()
        {
            if (!string.IsNullOrWhiteSpace(NewSymbol))
            {
                // If user typed "GOOG" and hit enter without selecting from popup
                // We need to fetch the name.
                var (symbol, name) = await _stockService.GetStockDetailsAsync(NewSymbol);
                if (!string.IsNullOrEmpty(symbol))
                {
                    AddStockInternal(symbol, name);
                }
            }
        }

        [RelayCommand]
        private void SelectSearchResult(StockSearchResult result)
        {
            if (result != null)
            {
                AddStockInternal(result.Symbol, result.Name);
            }
        }

        private void AddStockInternal(string symbol, string? name = null)
        {
            // Check if already exists
            if (Stocks.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                NewSymbol = string.Empty;
                IsSearchPopupOpen = false;
                return;
            }

            var stock = _stockService.CreateStock(symbol, name);
            Stocks.Add(stock);
            
            // Sync service
            _stockService.SetStocks(Stocks.ToList());
            
            SaveStocks();
            NewSymbol = string.Empty;
            IsSearchPopupOpen = false;
            
            // Re-sort
            ApplySortInternal();
        }

        [RelayCommand]
        private void RemoveStock(Stock stock)
        {
            if (stock != null)
            {
                Stocks.Remove(stock);
                _stockService.SetStocks(Stocks.ToList());
                SaveStocks();
            }
        }

        [RelayCommand]
        private void OpenStock(string symbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                try
                {
                    var url = $"https://www.google.com/search?q=stock+{symbol}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        [RelayCommand]
        private void SortBySymbol()
        {
            ApplySort("Symbol");
        }

        [RelayCommand]
        private void SortByName()
        {
            ApplySort("Name");
        }

        [RelayCommand]
        private void SortByChange()
        {
            ApplySort("Change");
        }

        [RelayCommand]
        private void SortByDayChangeValue()
        {
            ApplySort("DayChangeValue");
        }

        [RelayCommand]
        private void SortByMarketValue()
        {
            ApplySort("MarketValue");
        }

        [RelayCommand]
        private void UpdateShares(Stock stock)
        {
            SaveStocks();
            CalculatePortfolioTotals();
        }

        [RelayCommand]
        private void ExportData()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "PortfolioWatch_Export",
                DefaultExt = ".json",
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _settingsService.ExportStocks(dialog.FileName);
                    System.Windows.MessageBox.Show("Export successful!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ImportData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _settingsService.ImportStocks(dialog.FileName);
                    
                    // Reload data
                    var settings = _settingsService.LoadSettings();
                    _stockService.SetStocks(settings.Stocks);
                    Stocks = new ObservableCollection<Stock>(settings.Stocks);
                    
                    // Re-subscribe
                    foreach (var stock in Stocks)
                    {
                        stock.PropertyChanged += Stock_PropertyChanged;
                    }
                    
                    CalculatePortfolioTotals();
                    ApplySortInternal();
                    
                    System.Windows.MessageBox.Show("Import successful!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CalculatePortfolioTotals()
        {
            if (!IsPortfolioMode) return;

            decimal totalValue = 0;
            decimal totalDayChangeValue = 0;

            foreach (var stock in Stocks)
            {
                totalValue += stock.MarketValue;
                totalDayChangeValue += stock.DayChangeValue;
            }

            TotalPortfolioValue = totalValue;
            TotalPortfolioChange = totalDayChangeValue;
            
            // Calculate percent change based on previous day's total value
            // Previous Total = Current Total - Total Change
            decimal previousTotalValue = totalValue - totalDayChangeValue;
            
            if (previousTotalValue != 0)
            {
                TotalPortfolioChangePercent = (double)(totalDayChangeValue / previousTotalValue) * 100;
            }
            else
            {
                TotalPortfolioChangePercent = 0;
            }

            IsPortfolioUp = TotalPortfolioChange >= 0;
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task Reset()
        {
            if (System.Windows.MessageBox.Show(
                "Are you sure you want to reset all settings and portfolios to default? This action cannot be undone.",
                "Confirm Reset",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            // Clear settings
            var settings = new AppSettings(); // Default settings
            _settingsService.SaveSettings(settings);

            // Reset properties
            WindowTitle = "Watchlist";
            SortProperty = string.Empty;
            IsAscending = true;
            IsIndexesVisible = true;
            IsPortfolioMode = false;

            // Load default stocks
            var defaultStocks = _stockService.GetDefaultStocks();
            
            // Force UI update by clearing and adding
            Stocks.Clear();
            foreach (var stock in defaultStocks)
            {
                Stocks.Add(stock);
            }
            
            _stockService.SetStocks(Stocks.ToList());

            // Save defaults
            SaveStocks();
            
            // Re-apply sort (default)
            ApplySortInternal();
            
            // Fetch fresh data
            await _stockService.UpdatePricesAsync();
        }

        private void ApplySort(string property)
        {
            if (SortProperty == property)
            {
                IsAscending = !IsAscending;
            }
            else
            {
                SortProperty = property;
                IsAscending = true;
                if (property == "Change" || property == "DayChangeValue" || property == "MarketValue") IsAscending = false;
            }

            ApplySortInternal();
            SaveStocks();
        }

        private void ApplySortInternal()
        {
            Func<Stock, object> keySelector = SortProperty switch
            {
                "Name" => s => s.Name,
                "Change" => s => s.ChangePercent,
                "MarketValue" => s => s.MarketValue,
                "DayChangeValue" => s => s.DayChangeValue,
                _ => s => s.Symbol
            };

            var sorted = IsAscending 
                ? Stocks.OrderBy(keySelector).ToList() 
                : Stocks.OrderByDescending(keySelector).ToList();
            
            Stocks = new ObservableCollection<Stock>(sorted);
            
            // Sync service so updates happen on sorted list (order doesn't matter for updates but good for consistency)
            _stockService.SetStocks(Stocks.ToList());

            OnPropertyChanged(nameof(SymbolSortIcon));
            OnPropertyChanged(nameof(NameSortIcon));
            OnPropertyChanged(nameof(ChangeSortIcon));
            OnPropertyChanged(nameof(DayChangeValueSortIcon));
            OnPropertyChanged(nameof(MarketValueSortIcon));
        }

    public class StockSearchResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayText => $"{Symbol} - {Name}";
    }
}
}
