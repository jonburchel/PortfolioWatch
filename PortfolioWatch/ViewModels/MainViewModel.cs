using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioWatch.Models;
using PortfolioWatch.Services;
using PortfolioWatch.Views;

namespace PortfolioWatch.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IStockService _stockService;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _earningsTimer;
        private readonly DispatcherTimer _newsTimer;
        private CancellationTokenSource? _searchCts;

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

        public double PortfolioPreviousClose => (double)(TotalPortfolioValue - TotalPortfolioChange);

        [ObservableProperty]
        private bool _isPortfolioUp;

        [ObservableProperty]
        private System.Collections.Generic.List<double> _portfolioHistory = new();

        [ObservableProperty]
        private System.Collections.Generic.List<DateTime> _portfolioTimestamps = new();

        [ObservableProperty]
        private double _portfolioDayProgress;

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private double _windowOpacity = 0.8;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSystemTheme))]
        [NotifyPropertyChangedFor(nameof(IsLightTheme))]
        [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
        private AppTheme _currentTheme = AppTheme.System;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _selectedRange = "1d";

        [ObservableProperty]
        private string _dayChangeLabel = "Day $";

        [ObservableProperty]
        private string _changePercentLabel = "Day %";

        partial void OnSelectedRangeChanged(string value)
        {
            switch (value)
            {
                case "1d":
                    DayChangeLabel = "Day $";
                    ChangePercentLabel = "Day %";
                    break;
                case "5d":
                    DayChangeLabel = "5D $";
                    ChangePercentLabel = "5D %";
                    break;
                case "1mo":
                    DayChangeLabel = "30D $";
                    ChangePercentLabel = "30D %";
                    break;
                case "1y":
                    DayChangeLabel = "1Y $";
                    ChangePercentLabel = "1Y %";
                    break;
                case "5y":
                    DayChangeLabel = "5Y $";
                    ChangePercentLabel = "5Y %";
                    break;
                case "10y":
                    DayChangeLabel = "10Y $";
                    ChangePercentLabel = "10Y %";
                    break;
                default:
                    DayChangeLabel = "Day $";
                    ChangePercentLabel = "Day %";
                    break;
            }
        }

        public bool IsSystemTheme => CurrentTheme == AppTheme.System;
        public bool IsLightTheme => CurrentTheme == AppTheme.Light;
        public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

        private string _lastTopLevelSort = "Symbol";
        private bool _lastTopLevelSortAscending = true;

        partial void OnStartWithWindowsChanged(bool value)
        {
            if (!IsBusy)
            {
                _settingsService.SetStartup(value);
            }
        }

        partial void OnWindowOpacityChanged(double value)
        {
            if (!IsBusy) SaveStocks();
        }

        partial void OnCurrentThemeChanged(AppTheme value)
        {
            if (!IsBusy)
            {
                App.CurrentApp.ApplyTheme(value);
                SaveStocks();
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
            // Cancel previous search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrWhiteSpace(value))
            {
                IsSearchPopupOpen = false;
                SearchResults.Clear();
                return;
            }

            try
            {
                // Debounce
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var searchResult = await _stockService.SearchStocksAsync(value);
                if (token.IsCancellationRequested) return;

                if (!searchResult.Success || searchResult.Data == null)
                {
                    StatusMessage = searchResult.ErrorMessage ?? "Search failed";
                    return;
                }

                SearchResults.Clear();
                var searchResultsList = new System.Collections.Generic.List<StockSearchResult>();

                foreach (var result in searchResult.Data)
                {
                    searchResultsList.Add(new StockSearchResult { Symbol = result.Symbol, Name = result.Name });
                }

                // Initial display without quotes
                foreach (var item in searchResultsList)
                {
                    SearchResults.Add(item);
                }
                IsSearchPopupOpen = SearchResults.Count > 0;

                if (SearchResults.Count > 0)
                {
                    // Fetch quotes asynchronously
                    var symbols = searchResultsList.Select(r => r.Symbol).Take(10); // Limit to top 10
                    var quotesResult = await _stockService.GetQuotesAsync(symbols);
                    
                    if (token.IsCancellationRequested) return;

                    if (quotesResult.Success && quotesResult.Data != null)
                    {
                        // Update existing results with quote data
                        foreach (var quote in quotesResult.Data)
                        {
                            var existing = SearchResults.FirstOrDefault(r => r.Symbol == quote.Symbol);
                            if (existing != null)
                            {
                                existing.Price = quote.Price;
                                existing.Change = quote.Change;
                                existing.ChangePercent = quote.ChangePercent;
                                
                                // Force UI update if needed
                                var index = SearchResults.IndexOf(existing);
                                if (index >= 0)
                                {
                                    SearchResults[index] = existing;
                                }
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
                StatusMessage = $"Search error: {ex.Message}";
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
            _updateService = new UpdateService();
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _timer.Tick += Timer_Tick;

            _earningsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(4)
            };
            _earningsTimer.Tick += EarningsTimer_Tick;

            _newsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(15)
            };
            _newsTimer.Tick += NewsTimer_Tick;
            
            LoadData();
            _timer.Start();
            _earningsTimer.Start();
            _newsTimer.Start();
        }

        private async void LoadData()
        {
            IsBusy = true;
            StatusMessage = "Loading data...";
            
            var settings = _settingsService.LoadSettings();
            
            // Restore sort settings
            SortProperty = settings.SortColumn;
            IsAscending = settings.SortAscending;
            
            if (SortProperty == "Symbol" || SortProperty == "Name" || SortProperty == "Change")
            {
                _lastTopLevelSort = SortProperty;
                _lastTopLevelSortAscending = IsAscending;
            }

            WindowTitle = settings.WindowTitle;
            
            // Load Indexes
            var indexesResult = await _stockService.GetIndexesAsync();
            if (indexesResult.Success && indexesResult.Data != null)
            {
                Indexes = new ObservableCollection<Stock>(indexesResult.Data);
            }
            else
            {
                StatusMessage = $"Failed to load indexes: {indexesResult.ErrorMessage ?? "Unknown error"}";
            }

            if (!settings.IsFirstRun && settings.Stocks != null)
            {
                // Use saved stocks (even if empty)
                _stockService.SetStocks(settings.Stocks);
                Stocks = new ObservableCollection<Stock>(settings.Stocks);
            }
            else
            {
                // First run or invalid settings: Use default stocks from service
                var defaultStocksResult = await _stockService.GetStocksAsync();
                if (defaultStocksResult.Success && defaultStocksResult.Data != null)
                {
                    Stocks = new ObservableCollection<Stock>(defaultStocksResult.Data);
                    // Save defaults
                    SaveStocks();
                }
                else
                {
                    StatusMessage = $"Failed to load default stocks: {defaultStocksResult.ErrorMessage ?? "Unknown error"}";
                    Stocks = new ObservableCollection<Stock>();
                }
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
            StartWithWindows = _settingsService.IsStartupEnabled();
            CurrentTheme = settings.Theme;
            WindowOpacity = settings.WindowOpacity;
            SelectedRange = settings.SelectedRange;

            // Apply sort
            ApplySortInternal();
            
            // Initial fetch - Update ALL data (Prices, Options, Insider, RVOL, etc.)
            var updateResult = await _stockService.UpdateAllDataAsync(SelectedRange);
            if (!updateResult.Success)
            {
                StatusMessage = $"Update failed: {updateResult.ErrorMessage ?? "Unknown error"}";
            }
            else
            {
                StatusMessage = $"Last updated: {DateTime.Now:T}";
            }
            
            CalculatePortfolioTotals();

            IsBusy = false;

            // Check for updates on startup
            _ = CheckForUpdates(isManual: false);
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
                    ApplySortInternal();
                }
                else if ((SortProperty == "MarketValue" || SortProperty == "DayChangeValue") && 
                         (e.PropertyName == nameof(Stock.Price) || e.PropertyName == nameof(Stock.Change)))
                {
                    // Re-sort if sorting by dynamic values
                    ApplySortInternal();
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
            settings.Theme = CurrentTheme;
            settings.WindowOpacity = WindowOpacity;
            settings.SelectedRange = SelectedRange;
            settings.IsFirstRun = false;
            _settingsService.SaveSettings(settings);
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // Regular price update
            var result = await _stockService.UpdatePricesAsync(SelectedRange);
            if (result.Success)
            {
                StatusMessage = $"Last updated: {DateTime.Now:T}";
            }
            else
            {
                StatusMessage = $"Update failed: {result.ErrorMessage ?? "Unknown error"}";
            }
            CalculatePortfolioTotals();
            ApplySortInternal();

            // Check for updates daily
            var settings = _settingsService.CurrentSettings;
            if (settings.IsUpdateCheckEnabled && (DateTime.Now - settings.LastUpdateCheck).TotalHours >= 24)
            {
                _ = CheckForUpdates(isManual: false);
            }

            // Periodically refresh ALL data (Options, Insider, RVOL) - e.g., every hour or once a day
            // For now, let's do it every hour to keep flags fresh during the day
            if (DateTime.Now.Minute == 0) 
            {
                _ = _stockService.UpdateAllDataAsync(SelectedRange);
            }
        }

        private async void EarningsTimer_Tick(object? sender, EventArgs e)
        {
            await _stockService.UpdateEarningsAsync();
        }

        private async void NewsTimer_Tick(object? sender, EventArgs e)
        {
            await _stockService.UpdateNewsAsync();
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Refresh()
        {
            StatusMessage = "Refreshing...";
            var result = await _stockService.UpdatePricesAsync(SelectedRange);
            if (result.Success)
            {
                StatusMessage = $"Last updated: {DateTime.Now:T}";
            }
            else
            {
                StatusMessage = $"Update failed: {result.ErrorMessage ?? "Unknown error"}";
            }
            ApplySortInternal();
            _ = _stockService.UpdateEarningsAsync();
            _ = _stockService.UpdateNewsAsync();
        }

        [RelayCommand]
        private async Task SetRange(string range)
        {
            if (SelectedRange != range)
            {
                SelectedRange = range;
                SaveStocks();
                await Refresh();
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task AddStock()
        {
            if (!string.IsNullOrWhiteSpace(NewSymbol))
            {
                // If user typed "GOOG" and hit enter without selecting from popup
                // We need to fetch the name.
                var result = await _stockService.GetStockDetailsAsync(NewSymbol);
                if (result.Success && result.Data != default)
                {
                    AddStockInternal(result.Data.Symbol, result.Data.Name);
                }
                else
                {
                    StatusMessage = result.ErrorMessage ?? "Failed to find stock details";
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

        private async void AddStockInternal(string symbol, string? name = null)
        {
            // Check if already exists
            if (Stocks.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                NewSymbol = string.Empty;
                IsSearchPopupOpen = false;
                StatusMessage = $"Stock {symbol} already exists in watchlist.";
                return;
            }

            var stock = _stockService.CreateStock(symbol, name, SelectedRange);
            Stocks.Add(stock);
            
            // Sync service
            _stockService.SetStocks(Stocks.ToList());
            
            SaveStocks();
            NewSymbol = string.Empty;
            IsSearchPopupOpen = false;
            StatusMessage = $"Added {symbol}... fetching data";
            
            // Re-sort
            ApplySortInternal();

            // Explicitly wait for full data update to ensure UI populates
            await _stockService.UpdateAllDataAsync(SelectedRange);
            StatusMessage = $"Added {symbol}";
        }

        [RelayCommand]
        private void RemoveStock(Stock stock)
        {
            if (stock != null && Stocks.Contains(stock))
            {
                stock.PropertyChanged -= Stock_PropertyChanged;
                Stocks.Remove(stock);
                _stockService.SetStocks(Stocks.ToList());
                SaveStocks();
                CalculatePortfolioTotals();
            }
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task Reset()
        {
            var confirmationWindow = new ConfirmationWindow(
                "Confirm Reset",
                "Are you sure you want to reset all settings and portfolios to default? This action cannot be undone.");

            if (confirmationWindow.ShowDialog() != true)
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
            CurrentTheme = AppTheme.System;
            WindowOpacity = 0.8;
            SelectedRange = "1d";

            // Load default stocks
            var defaultStocksResult = await _stockService.GetStocksAsync();
            if (defaultStocksResult.Success && defaultStocksResult.Data != null)
            {
                // Force UI update by clearing and adding
                Stocks.Clear();
                foreach (var stock in defaultStocksResult.Data)
                {
                    Stocks.Add(stock);
                }
                
                _stockService.SetStocks(Stocks.ToList());

                // Save defaults
                SaveStocks();
                
                // Re-apply sort (default)
                ApplySortInternal();
                
                // Fetch fresh data
                await _stockService.UpdatePricesAsync(SelectedRange);
                StatusMessage = "Reset complete.";
            }
            else
            {
                StatusMessage = $"Reset failed: {defaultStocksResult.ErrorMessage ?? "Unknown error"}";
            }
        }

        [RelayCommand]
        private void BuyCoffee(string amount)
        {
            if (amount == "0")
            {
                try
                {
                    var url = "mailto:jonburchel@gmail.com?subject=Thanks%20for%20PortfolioWatch!";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
                return;
            }

            if (amount == "Custom")
            {
                var inputWindow = new InputWindow("Enter the amount you'd like to contribute:", "Enter Amount", "$1,000,000");
                if (inputWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputWindow.InputText))
                {
                    // Strip currency symbol and commas
                    var cleanAmount = inputWindow.InputText.Replace("$", "").Replace(",", "");
                    if (decimal.TryParse(cleanAmount, out decimal customVal))
                    {
                        BuyCoffee(customVal.ToString());
                    }
                }
                return;
            }

            // Validate amount is integer > 1
            if (decimal.TryParse(amount, out decimal val) && val >= 1 && val <= int.MaxValue)
            {
                int intVal = (int)val;
                if (intVal < 1) intVal = 1;

                try
                {
                    var url = $"https://venmo.com/?txn=pay&recipients=Jon-Burchel&amount={intVal}&note=Buy%20me%20a%20coffee";
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
        private void OpenAbout()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/jonburchel/PortfolioWatch",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        [RelayCommand]
        private void RequestFeature()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/jonburchel/PortfolioWatch/issues/new",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        [RelayCommand]
        private void SetTheme(string theme)
        {
            if (Enum.TryParse<AppTheme>(theme, out var appTheme))
            {
                CurrentTheme = appTheme;
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
        private async Task CheckForUpdates()
        {
            await CheckForUpdates(isManual: true);
        }

        private async Task CheckForUpdates(bool isManual)
        {
            var settings = _settingsService.CurrentSettings;

            // If automatic check, verify if we should check
            if (!isManual)
            {
                if (!settings.IsUpdateCheckEnabled) return;
                
                if (settings.UpdateSnoozedUntil.HasValue && DateTime.Now < settings.UpdateSnoozedUntil.Value)
                    return;
            }

            // Update last check time
            settings.LastUpdateCheck = DateTime.Now;
            _settingsService.SaveSettings(settings);

            if (isManual) StatusMessage = "Checking for updates...";

            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo.IsUpdateAvailable)
            {
                if (isManual) StatusMessage = "Update available!";

                var prompt = new UpdatePromptWindow
                {
                    Message = $"A new version (Portfolio Watch {updateInfo.Version}) is available, released on {updateInfo.ReleaseDate:d}."
                };

                prompt.ShowDialog();

                switch (prompt.Result)
                {
                    case UpdatePromptResult.Update:
                        StatusMessage = "Downloading update...";
                        await _updateService.ApplyUpdateAsync(updateInfo.DownloadUrl, updateInfo.FileName);
                        break;
                    
                    case UpdatePromptResult.Snooze:
                        settings.UpdateSnoozedUntil = DateTime.Now.AddDays(7);
                        _settingsService.SaveSettings(settings);
                        break;
                    
                    case UpdatePromptResult.Disable:
                        settings.IsUpdateCheckEnabled = false;
                        _settingsService.SaveSettings(settings);
                        break;
                }
            }
            else
            {
                if (isManual)
                {
                    StatusMessage = "You are up to date.";
                    var prompt = new UpdatePromptWindow
                    {
                        Title = "Up to Date",
                        Message = "You are running the latest version.",
                        IsInfoMode = true
                    };
                    prompt.ShowDialog();
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
            
            var portfolioHistory = new System.Collections.Generic.List<double>();
            var portfolioTimestamps = new System.Collections.Generic.List<DateTime>();
            int maxHistoryCount = 0;
            double maxDayProgress = 0;
            Stock? stockWithMaxHistory = null;

            foreach (var stock in Stocks)
            {
                totalValue += stock.MarketValue;
                totalDayChangeValue += stock.DayChangeValue;

                if (stock.Shares > 0 && stock.History != null)
                {
                    if (stock.History.Count > maxHistoryCount)
                    {
                        maxHistoryCount = stock.History.Count;
                        stockWithMaxHistory = stock;
                    }
                    maxDayProgress = Math.Max(maxDayProgress, stock.DayProgress);
                }
            }

            // Aggregate history
            // 1. Collect all unique timestamps
            var allTimestamps = new System.Collections.Generic.HashSet<DateTime>();
            foreach (var stock in Stocks)
            {
                if (stock.Shares > 0 && stock.Timestamps != null)
                {
                    foreach (var ts in stock.Timestamps)
                    {
                        allTimestamps.Add(ts);
                    }
                }
            }

            if (allTimestamps.Count > 0)
            {
                portfolioTimestamps = allTimestamps.OrderBy(t => t).ToList();
                
                // 2. Calculate total value at each timestamp
                foreach (var ts in portfolioTimestamps)
                {
                    double pointValue = 0;
                    foreach (var stock in Stocks)
                    {
                        if (stock.Shares > 0)
                        {
                            double priceAtTime = 0;
                            
                            if (stock.Timestamps != null && stock.History != null && stock.Timestamps.Count > 0)
                            {
                                // Find exact match or last known price before this timestamp
                                // Since lists are sorted, we could optimize this, but for now LINQ is safer/easier
                                int index = stock.Timestamps.FindLastIndex(t => t <= ts);
                                
                                if (index >= 0 && index < stock.History.Count)
                                {
                                    priceAtTime = stock.History[index];
                                }
                                else if (stock.History.Count > 0)
                                {
                                    // If timestamp is before first history point, use first point? 
                                    // Or 0? Let's use first point (flat line start)
                                    priceAtTime = stock.History[0];
                                }
                            }
                            
                            if (priceAtTime == 0)
                            {
                                // Fallback to current price if no history found (e.g. new listing or error)
                                priceAtTime = (double)stock.Price;
                            }

                            pointValue += priceAtTime * (double)stock.Shares;
                        }
                    }
                    portfolioHistory.Add(pointValue);
                }
            }
            else
            {
                // No history at all, create a single point or flat line based on current values?
                // If we have no timestamps, we can't draw a graph.
            }

            PortfolioHistory = portfolioHistory;
            PortfolioTimestamps = portfolioTimestamps;
            PortfolioDayProgress = maxDayProgress;

            TotalPortfolioValue = totalValue;
            TotalPortfolioChange = totalDayChangeValue;
            OnPropertyChanged(nameof(PortfolioPreviousClose));
            
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

            // Calculate individual stock percentages
            foreach (var stock in Stocks)
            {
                if (totalValue > 0)
                {
                    stock.PortfolioPercentage = (double)(stock.MarketValue / totalValue);
                }
                else
                {
                    stock.PortfolioPercentage = 0;
                }
            }
        }

        [RelayCommand]
        private void OpenNews(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to open link: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void OpenStock(Stock stock)
        {
            if (stock != null && !string.IsNullOrWhiteSpace(stock.Symbol))
            {
                try
                {
                    var url = $"https://finance.yahoo.com/quote/{stock.Symbol}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to open link: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void Exit()
        {
            System.Windows.Application.Current.Shutdown();
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

            if (property == "Symbol" || property == "Name" || property == "Change")
            {
                _lastTopLevelSort = property;
                _lastTopLevelSortAscending = IsAscending;
            }

            ApplySortInternal();
            SaveStocks();
        }

        private void ApplySortInternal()
        {
            if (SortProperty == "DayChangeValue" || SortProperty == "MarketValue")
            {
                var withShares = Stocks.Where(s => s.Shares > 0);
                var withoutShares = Stocks.Where(s => s.Shares == 0);

                Func<Stock, object> keySelector = SortProperty == "DayChangeValue"
                    ? s => s.DayChangeValue
                    : s => s.MarketValue;

                var sortedWithShares = IsAscending
                    ? withShares.OrderBy(keySelector)
                    : withShares.OrderByDescending(keySelector);

                // For portfolio sorts, 0-share rows sort by Day % in the same direction
                Func<Stock, object> secondaryKeySelector = s => s.ChangePercent;

                var sortedWithoutShares = IsAscending
                    ? withoutShares.OrderBy(secondaryKeySelector)
                    : withoutShares.OrderByDescending(secondaryKeySelector);

                Stocks = new ObservableCollection<Stock>(sortedWithShares.Concat(sortedWithoutShares));
            }
            else
            {
                Func<Stock, object> keySelector = SortProperty switch
                {
                    "Name" => s => s.Name,
                    "Change" => s => s.ChangePercent,
                    _ => s => s.Symbol
                };

                var sorted = IsAscending
                    ? Stocks.OrderBy(keySelector).ToList()
                    : Stocks.OrderByDescending(keySelector).ToList();

                Stocks = new ObservableCollection<Stock>(sorted);
            }
            
            // Sync service so updates happen on sorted list (order doesn't matter for updates but good for consistency)
            _stockService.SetStocks(Stocks.ToList());

            OnPropertyChanged(nameof(SymbolSortIcon));
            OnPropertyChanged(nameof(NameSortIcon));
            OnPropertyChanged(nameof(ChangeSortIcon));
            OnPropertyChanged(nameof(DayChangeValueSortIcon));
            OnPropertyChanged(nameof(MarketValueSortIcon));
        }

    }
}
