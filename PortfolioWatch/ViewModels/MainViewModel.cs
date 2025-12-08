using System;
using System.Collections.Generic;
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
        private ObservableCollection<PortfolioTabViewModel> _tabs = new();

        [ObservableProperty]
        private PortfolioTabViewModel? _selectedTab;

        [ObservableProperty]
        private ObservableCollection<Stock> _stocks = new ObservableCollection<Stock>();

        [ObservableProperty]
        private ObservableCollection<Stock> _indexes = new ObservableCollection<Stock>();

        [ObservableProperty]
        private ObservableCollection<StockSearchResult> _searchResults = new ObservableCollection<StockSearchResult>();

        [ObservableProperty]
        private bool _isBusy;

        private bool _isLoading;

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
        private double _uIScale = 1.0;

        partial void OnUIScaleChanged(double value)
        {
            if (!_isLoading) SaveStocks();
        }

        [RelayCommand]
        private void ResetScale() => UIScale = 1.0;

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

        [ObservableProperty]
        private bool? _isAllIncluded = true;

        [ObservableProperty]
        private bool _isSingleTab;

        [ObservableProperty]
        private bool _isMultiTabSelection;

        [ObservableProperty]
        private bool _isMergedView;

        [ObservableProperty]
        private ObservableCollection<Stock> _mergedStocks = new();

        [ObservableProperty]
        private ObservableCollection<TaxAllocation> _aggregateTaxAllocations = new();

        public event EventHandler? RequestSearchFocus;

        partial void OnIsMergedViewChanged(bool value)
        {
            if (value)
            {
                GenerateMergedView();
                Stocks = MergedStocks;
            }
            else
            {
                if (SelectedTab != null)
                {
                    Stocks = SelectedTab.Stocks;
                }
            }
            CalculatePortfolioTotals();
        }

        private void GenerateMergedView()
        {
            var includedTabs = Tabs.Where(t => !t.IsAddButton && t.IsIncludedInTotal).ToList();
            var allStocks = includedTabs.SelectMany(t => t.Stocks).ToList();

            var grouped = allStocks.GroupBy(s => s.Symbol);
            var mergedList = new System.Collections.Generic.List<Stock>();

            foreach (var group in grouped)
            {
                var first = group.First();
                var totalShares = group.Sum(s => s.Shares);

                // Create a new stock instance for the merged view
                // We clone the first one to get all properties (History, etc.)
                // then update the shares.
                var mergedStock = first.Clone();
                mergedStock.Shares = totalShares;
                
                // Ensure calculated fields are correct
                // (Clone copies properties, but Shares setter triggers recalculations)
                
                mergedList.Add(mergedStock);
            }

            MergedStocks = new ObservableCollection<Stock>(mergedList);
            
            // If we are currently in merged view, update the main Stocks collection reference
            if (IsMergedView)
            {
                Stocks = MergedStocks;
                ApplySortInternal(); // Re-apply sort to the new merged list
            }
        }

        partial void OnSelectedTabChanged(PortfolioTabViewModel? oldValue, PortfolioTabViewModel? newValue)
        {
            if (oldValue != null && !oldValue.IsAddButton)
            {
                // Restore the previous state of the old tab
                oldValue.RestoreIncludedState();
            }

            if (newValue != null)
            {
                if (newValue.IsAddButton)
                {
                    AddTab();
                    return;
                }

                // Save the current state of the new tab before forcing it to true
                newValue.SaveIncludedState();
                
                // Always include the selected tab
                newValue.IsIncludedInTotal = true;
                
                UpdateAllIncludedState();

                if (!IsMergedView)
                {
                    Stocks = newValue.Stocks;
                }
                
                UpdateServiceStocks();
                
                CalculatePortfolioTotals();
                ApplySortInternal();
                if (!_isLoading) SaveStocks();
            }
        }

        [RelayCommand]
        private void ToggleAllIncluded()
        {
            if (IsAllIncluded == true)
            {
                // Fully Checked -> Uncheck All (Empty)
                foreach (var tab in Tabs)
                {
                    if (!tab.IsAddButton)
                    {
                        tab.IsIncludedInTotal = false;
                    }
                }
            }
            else
            {
                // Indeterminate (null) or Unchecked (false) -> Check All (Solid)
                foreach (var tab in Tabs)
                {
                    if (!tab.IsAddButton)
                    {
                        tab.IsIncludedInTotal = true;
                    }
                }
            }
            UpdateAllIncludedState();
            CalculatePortfolioTotals();
            SaveStocks();
        }

        private void UpdateAllIncludedState()
        {
            var validTabs = Tabs.Where(t => !t.IsAddButton).ToList();
            if (validTabs.Count == 0) 
            {
                IsAllIncluded = false;
                IsMultiTabSelection = false;
                return;
            }

            bool allChecked = validTabs.All(t => t.IsIncludedInTotal);
            var checkedTabs = validTabs.Where(t => t.IsIncludedInTotal).ToList();
            IsMultiTabSelection = checkedTabs.Count > 1;
            
            if (allChecked)
            {
                IsAllIncluded = true;
            }
            else
            {
                // Check if ONLY the active tab is checked
                if (checkedTabs.Count == 1 && checkedTabs[0] == SelectedTab)
                {
                    IsAllIncluded = false; // Empty check (Only active tab included)
                }
                else
                {
                    // Mixed state (some checked, not just active)
                    // Requirement says "can only ever be a solid check ... or a gray check".
                    // We'll treat mixed as gray/indeterminate for now as it's not "All".
                    IsAllIncluded = null; 
                }
            }
        }

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
            if (!_isLoading) SaveStocks();
        }

        public bool IsSystemTheme => CurrentTheme == AppTheme.System;
        public bool IsLightTheme => CurrentTheme == AppTheme.Light;
        public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

        private string _lastTopLevelSort = "Symbol";
        private bool _lastTopLevelSortAscending = true;

        partial void OnStartWithWindowsChanged(bool value)
        {
            if (!_isLoading)
            {
                _settingsService.SetStartup(value);
            }
        }

        partial void OnWindowOpacityChanged(double value)
        {
            if (!_isLoading) SaveStocks();
        }

        partial void OnCurrentThemeChanged(AppTheme value)
        {
            if (!_isLoading)
            {
                App.CurrentApp.ApplyTheme(value);
                SaveStocks();
            }
        }

        partial void OnWindowTitleChanged(string value)
        {
            if (!_isLoading) SaveStocks();
        }

        partial void OnIsIndexesVisibleChanged(bool value)
        {
            if (!_isLoading) SaveStocks();
        }

        partial void OnIsPortfolioModeChanged(bool value)
        {
            if (!_isLoading) SaveStocks();
            CalculatePortfolioTotals();
        }

        partial void OnSortPropertyChanged(string value)
        {
            if (!_isLoading) SaveStocks();
        }

        partial void OnIsAscendingChanged(bool value)
        {
            if (!_isLoading) SaveStocks();
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
            
            // LoadData(); // Deferred to Initialize()
            _timer.Start();
            _earningsTimer.Start();
            _newsTimer.Start();

            // Initialize with a placeholder tab to avoid empty TabControl issues
            var placeholderTab = new PortfolioTabViewModel(new PortfolioTab { Name = "Loading..." });
            Tabs.Add(placeholderTab);
            SelectedTab = placeholderTab;

            Tabs.CollectionChanged += Tabs_CollectionChanged;
            UpdateIsSingleTab();
        }

        private void Tabs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateIsSingleTab();
            UpdateAllIncludedState();
            CalculatePortfolioTotals();
        }

        private void UpdateIsSingleTab()
        {
            IsSingleTab = Tabs.Count(t => !t.IsAddButton) <= 1;
        }

        [RelayCommand]
        private void DuplicateTab(PortfolioTabViewModel tab)
        {
            if (tab == null || tab.IsAddButton) return;

            var newTab = new PortfolioTabViewModel(new PortfolioTab 
            { 
                Name = $"{tab.Name} (Copy)",
                Stocks = new System.Collections.Generic.List<Stock>(tab.Stocks.Select(s => s.Clone())),
                TaxAllocations = new System.Collections.Generic.List<TaxAllocation>(tab.TaxAllocations.Select(t => new TaxAllocation { Type = t.Type, Percentage = t.Percentage }))
            });

            // Subscribe to events
            newTab.PropertyChanged += Tab_PropertyChanged;
            newTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
            newTab.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
            foreach (var stock in newTab.Stocks)
            {
                stock.PropertyChanged += Stock_PropertyChanged;
            }

            // Insert before the Add Button (last index)
            if (Tabs.Count > 0)
            {
                Tabs.Insert(Tabs.Count - 1, newTab);
            }
            else
            {
                Tabs.Add(newTab);
            }

            SelectedTab = newTab;
            UpdateServiceStocks();
            SaveStocks();
        }

        public void Initialize()
        {
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                // Yield execution to allow UI to render immediately
                await Dispatcher.Yield();

                _isLoading = true;
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

            // Load Tabs
            Tabs.Clear();
            if (settings.Tabs != null && settings.Tabs.Count > 0)
            {
                foreach (var tab in settings.Tabs)
                {
                    var tabVm = new PortfolioTabViewModel(tab);
                    // Subscribe to events
                    tabVm.PropertyChanged += Tab_PropertyChanged;
                    tabVm.Stocks.CollectionChanged += Stocks_CollectionChanged;
                    tabVm.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
                    foreach (var stock in tabVm.Stocks)
                    {
                        stock.PropertyChanged += Stock_PropertyChanged;
                    }
                    Tabs.Add(tabVm);
                }
            }
            else
            {
                // Should be handled by SettingsService migration, but fallback here
                var defaultTab = new PortfolioTabViewModel(new PortfolioTab { Name = "Default portfolio watchlist" });
                
                // Try to load legacy stocks or defaults
                if (!settings.IsFirstRun && settings.Stocks != null && settings.Stocks.Count > 0)
                {
                    foreach (var s in settings.Stocks) defaultTab.Stocks.Add(s);
                }
                else
                {
                    var defaultStocksResult = await _stockService.GetStocksAsync();
                    if (defaultStocksResult.Success && defaultStocksResult.Data != null)
                    {
                        foreach (var s in defaultStocksResult.Data) defaultTab.Stocks.Add(s);
                    }
                }
                
                defaultTab.PropertyChanged += Tab_PropertyChanged;
                defaultTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
                defaultTab.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
                foreach (var stock in defaultTab.Stocks)
                {
                    stock.PropertyChanged += Stock_PropertyChanged;
                }
                Tabs.Add(defaultTab);
            }

            // Add the "New Tab" button placeholder
            Tabs.Add(new PortfolioTabViewModel(true));

            // Select tab based on saved index
            if (settings.SelectedTabIndex >= 0 && settings.SelectedTabIndex < Tabs.Count)
            {
                SelectedTab = Tabs[settings.SelectedTabIndex];
            }
            else
            {
                SelectedTab = Tabs.FirstOrDefault();
            }

            if (SelectedTab != null)
            {
                Stocks = SelectedTab.Stocks;
            }
            
            UpdateServiceStocks();

            // Restore IsIndexesVisible after stocks are loaded to prevent overwriting with empty list
            IsIndexesVisible = settings.IsIndexesVisible;
            IsPortfolioMode = settings.IsPortfolioMode;
            StartWithWindows = _settingsService.IsStartupEnabled();
            CurrentTheme = settings.Theme;
            WindowOpacity = settings.WindowOpacity;
            UIScale = settings.UIScale;
            SelectedRange = settings.SelectedRange;

            // Apply sort
            ApplySortInternal();
            
            // Unblock UI immediately with cached data
            IsBusy = false;
            _isLoading = false;

            // Initial fetch - Update Prices in background
            // We don't await this to allow the UI to be responsive immediately
            _ = Task.Run(async () => 
            {
                var updateResult = await _stockService.UpdatePricesAsync(SelectedRange);
                
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    if (!updateResult.Success)
                    {
                        StatusMessage = $"Update failed: {updateResult.ErrorMessage ?? "Unknown error"}";
                    }
                    else
                    {
                        StatusMessage = $"Last updated: {DateTime.Now:T}";
                    }
                    
                    CalculatePortfolioTotals();
                    ApplySortInternal();
                });

                // Update auxiliary data in background (Earnings, News, Options, Insider, RVOL)
                await _stockService.UpdateAuxiliaryDataAsync();
            });

            // Check for updates on startup
            _ = CheckForUpdates(isManual: false);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                Debug.WriteLine($"LoadData Error: {ex}");
                _isLoading = false;
                IsBusy = false;
            }
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

        private void Tab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PortfolioTabViewModel.IsIncludedInTotal))
            {
                // Enforce: Active tab CANNOT be unchecked
                if (sender is PortfolioTabViewModel tab && tab == SelectedTab && !tab.IsIncludedInTotal)
                {
                    tab.IsIncludedInTotal = true;
                    return; // UpdateAllIncludedState will be called when we set it back to true
                }

                UpdateAllIncludedState();
                CalculatePortfolioTotals();
                if (!_isLoading) SaveStocks();
            }
            else if (e.PropertyName == nameof(PortfolioTabViewModel.TaxAllocations))
            {
                CalculatePortfolioTotals();
                SaveStocks();
            }
        }

        public void UpdateTabTaxAllocations(PortfolioTabViewModel tabVm, IEnumerable<TaxAllocation> allocations)
        {
            // Create new collection to trigger property change for converters
            var newAllocations = new ObservableCollection<TaxAllocation>();
            foreach (var allocation in allocations)
            {
                newAllocations.Add(allocation);
            }
            tabVm.TaxAllocations = newAllocations;

            SaveStocks();
            CalculatePortfolioTotals(); // Re-calculate aggregate
        }

        private void Tab_RequestEditTaxStatus(object? sender, EventArgs e)
        {
            // Deprecated: Handled by View for better positioning
        }

        private void UpdateServiceStocks()
        {
            var allStocks = new System.Collections.Generic.List<Stock>();
            foreach (var tab in Tabs)
            {
                allStocks.AddRange(tab.Stocks);
            }
            _stockService.SetStocks(allStocks);
        }

        private void SaveStocks()
        {
            var settings = _settingsService.CurrentSettings;
            SyncViewModelToSettings(settings);
            _settingsService.SaveSettings(settings);
        }

        public void SaveWindowPositions(double left, double top, double width, double height)
        {
            var settings = _settingsService.CurrentSettings;
            settings.WindowLeft = left;
            settings.WindowTop = top;
            settings.WindowWidth = width;
            settings.WindowHeight = height;
            
            SyncViewModelToSettings(settings);
            _settingsService.SaveSettings(settings);
        }

        private void SyncViewModelToSettings(AppSettings settings)
        {
            // Save Tabs (exclude the Add Button placeholder)
            settings.Tabs = Tabs.Where(t => !t.IsAddButton).Select(t => t.ToModel()).ToList();
            
            // Legacy support (save current tab stocks to root stocks)
            if (SelectedTab != null && !SelectedTab.IsAddButton)
            {
                settings.Stocks = SelectedTab.Stocks.ToList();
            }
            
            settings.SortColumn = SortProperty;
            settings.SortAscending = IsAscending;
            settings.WindowTitle = WindowTitle;
            settings.IsIndexesVisible = IsIndexesVisible;
            settings.IsPortfolioMode = IsPortfolioMode;
            settings.Theme = CurrentTheme;
            settings.WindowOpacity = WindowOpacity;
            settings.UIScale = UIScale;
            settings.SelectedRange = SelectedRange;
            settings.SelectedTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
            settings.IsFirstRun = false;
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
        private void SelectSearchResult(StockSearchResult result)
        {
            if (result != null)
            {
                AddStockInternal(result.Symbol, result.Name);
            }
        }

        private async void AddStockInternal(string symbol, string name)
        {
            if (SelectedTab == null || SelectedTab.IsAddButton) return;

            // Check if already exists in current tab
            if (SelectedTab.Stocks.Any(s => s.Symbol == symbol))
            {
                StatusMessage = $"{symbol} is already in the list.";
                NewSymbol = string.Empty;
                IsSearchPopupOpen = false;
                return;
            }

            var stock = new Stock
            {
                Symbol = symbol,
                Name = name,
                Price = 0,
                Change = 0,
                ChangePercent = 0
            };

            // Subscribe to events
            stock.PropertyChanged += Stock_PropertyChanged;

            SelectedTab.Stocks.Add(stock);
            NewSymbol = string.Empty;
            IsSearchPopupOpen = false;
            
            SaveStocks();
            UpdateServiceStocks();

            // Fetch data immediately
            var result = await _stockService.GetQuotesAsync(new[] { symbol });
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                var quote = result.Data[0];
                stock.Price = (decimal)(quote.Price ?? 0);
                stock.Change = (decimal)(quote.Change ?? 0);
                stock.ChangePercent = quote.ChangePercent ?? 0;
                
                // Also fetch auxiliary data
                await _stockService.UpdateAuxiliaryDataAsync();
            }
            
            ApplySortInternal();
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
        private void RemoveStock(Stock stock)
        {
            if (stock == null || SelectedTab == null || SelectedTab.IsAddButton) return;

            SelectedTab.Stocks.Remove(stock);
            UpdateServiceStocks();
            SaveStocks();
        }

        [RelayCommand]
        public async Task Reset()
        {
            var confirmationWindow = new ConfirmationWindow("Reset Application", "Are you sure you want to reset all settings and data? This cannot be undone.", showResetOption: true, isAlert: false);
            if (confirmationWindow.ShowDialog() != true) return;

            if (confirmationWindow.ResetSettings)
            {
                // Clear settings
                var settings = new AppSettings(); // Default settings
                _settingsService.SaveSettings(settings);

                // Reset properties
                WindowTitle = "Your watchlist";
                SortProperty = string.Empty;
                IsAscending = true;
                IsIndexesVisible = true;
                IsPortfolioMode = false;
                CurrentTheme = AppTheme.System;
                WindowOpacity = 0.8;
                UIScale = 1.0;
                SelectedRange = "1d";
                
                // Reset window positions
                App.CurrentApp.ResetWindowPositions();
            }

            // Reset Tabs
            Tabs.Clear();
            var defaultTab = new PortfolioTabViewModel(new PortfolioTab { Name = "Default watchlist" });
            Tabs.Add(defaultTab);
            
            // Add the "New Tab" button placeholder
            Tabs.Add(new PortfolioTabViewModel(true));

            SelectedTab = defaultTab;
            defaultTab.IsEditing = true;

            // Load default stocks (hardcoded defaults)
            var defaultStocks = _stockService.GetDefaultStocks();
            
            foreach (var stock in defaultStocks)
            {
                stock.Shares = 0; // Ensure shares are 0
                defaultTab.Stocks.Add(stock);
                stock.PropertyChanged += Stock_PropertyChanged;
            }
            defaultTab.PropertyChanged += Tab_PropertyChanged;
            defaultTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
            
            UpdateServiceStocks();

            // Save defaults
            SaveStocks();
            
            // Re-apply sort (default)
            ApplySortInternal();
            
            // Show success immediately
            StatusMessage = "Reset complete.";
            var alert = new ConfirmationWindow("Success", "Reset complete!", isAlert: true);
            alert.ShowDialog();

            // Fetch fresh data in background
            await _stockService.UpdateAllDataAsync(SelectedRange);
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
            SaveStocks(); // Ensure latest changes (like tab renames) are persisted before export

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
                    _settingsService.ExportStocks(dialog.FileName, portfolioName: WindowTitle);
                    new ConfirmationWindow("Success", "Export successful!", isAlert: true, icon: "✅").ShowDialog();
                }
                catch (Exception ex)
                {
                    new ConfirmationWindow("Error", $"Export failed: {ex.Message}", isAlert: true, icon: "❌").ShowDialog();
                }
            }
        }

        [RelayCommand]
        private void ExportNormalizedData()
        {
            SaveStocks(); // Ensure latest changes are persisted

            if (TotalPortfolioValue <= 0)
            {
                new ConfirmationWindow("Error", "Cannot normalize an empty portfolio.", isAlert: true, icon: "⚠️").ShowDialog();
                return;
            }

            var inputWindow = new InputWindow(
                "Enter the target portfolio value for normalization:", 
                "Export normalized portfolio", 
                "1,000,000",
                input => 
                {
                    var clean = input.Replace("$", "").Replace(",", "");
                    return decimal.TryParse(clean, out decimal val) && val > 0 ? null : "Please enter a valid positive number.";
                });

            if (inputWindow.ShowDialog() == true)
            {
                var cleanAmount = inputWindow.InputText.Replace("$", "").Replace(",", "");
                if (decimal.TryParse(cleanAmount, out decimal targetValue))
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"PortfolioWatch_Normalized_{targetValue:0}Export",
                        DefaultExt = ".json",
                        Filter = "JSON Files (*.json)|*.json"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        try
                        {
                            var normalizedStocks = new System.Collections.Generic.List<Stock>();
                            
                            foreach (var stock in Stocks)
                            {
                                double newShares = 0;

                                if (stock.Shares > 0 && stock.Price > 0)
                                {
                                    // Calculate weight in current portfolio
                                    decimal weight = stock.MarketValue / TotalPortfolioValue;
                                    
                                    // Calculate new shares to maintain weight in target portfolio
                                    // TargetStockValue = TargetPortfolioValue * Weight
                                    // NewShares = TargetStockValue / Price
                                    decimal targetStockValue = targetValue * weight;
                                    newShares = (double)(targetStockValue / stock.Price);
                                }

                                // Create a copy with normalized shares (or 0 for watchlist items)
                                var normalizedStock = new Stock
                                {
                                    Symbol = stock.Symbol,
                                    Name = stock.Name,
                                    Price = stock.Price,
                                    Change = stock.Change,
                                    ChangePercent = stock.ChangePercent,
                                    Shares = newShares
                                };
                                normalizedStocks.Add(normalizedStock);
                            }

                            string normalizedName = $"{WindowTitle} (Normalized to {targetValue:C0})";
                            
                            // Determine tax allocations to include
                            IEnumerable<TaxAllocation> taxAllocations;
                            if (IsMergedView)
                            {
                                taxAllocations = AggregateTaxAllocations;
                            }
                            else if (SelectedTab != null)
                            {
                                taxAllocations = SelectedTab.TaxAllocations;
                            }
                            else
                            {
                                taxAllocations = new[] { new TaxAllocation { Type = TaxStatusType.Unspecified, Percentage = 100 } };
                            }

                            _settingsService.ExportStocks(dialog.FileName, normalizedStocks, normalizedName, taxAllocations);
                            new ConfirmationWindow("Success", $"Export successful! Portfolio normalized to {targetValue:C0}.", isAlert: true, icon: "✅").ShowDialog();
                        }
                        catch (Exception ex)
                        {
                            new ConfirmationWindow("Error", $"Export failed: {ex.Message}", isAlert: true, icon: "❌").ShowDialog();
                        }
                    }
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
                    var importedSettings = _settingsService.ParseImportFile(dialog.FileName);
                    if (importedSettings == null || importedSettings.Tabs.Count == 0)
                    {
                        throw new Exception("No valid portfolio data found in file.");
                    }

                    var prompt = new ImportPromptWindow();
                    if (prompt.ShowDialog() != true || prompt.Result == ImportAction.Cancel)
                    {
                        return;
                    }

                    if (prompt.Result == ImportAction.Replace)
                    {
                        Tabs.Clear();
                        WindowTitle = importedSettings.WindowTitle;
                    }

                    foreach (var tab in importedSettings.Tabs)
                    {
                        var tabVm = new PortfolioTabViewModel(tab);
                        tabVm.PropertyChanged += Tab_PropertyChanged;
                        tabVm.Stocks.CollectionChanged += Stocks_CollectionChanged;
                        foreach (var stock in tabVm.Stocks)
                        {
                            stock.PropertyChanged += Stock_PropertyChanged;
                        }
                        
                        // Insert before the Add Button (last index) if it exists
                        if (Tabs.Count > 0 && Tabs.Last().IsAddButton)
                        {
                            Tabs.Insert(Tabs.Count - 1, tabVm);
                        }
                        else
                        {
                            Tabs.Add(tabVm);
                        }
                    }

                    // Ensure Add Button exists if we replaced everything
                    if (!Tabs.Any(t => t.IsAddButton))
                    {
                        Tabs.Add(new PortfolioTabViewModel(true));
                    }

                    // Disable Merged View on import
                    IsMergedView = false;

                    // Set active tab
                    if (Tabs.Count > 0)
                    {
                        // Try to restore selected index from import if valid
                        if (importedSettings.SelectedTabIndex >= 0 && importedSettings.SelectedTabIndex < Tabs.Count && !Tabs[importedSettings.SelectedTabIndex].IsAddButton)
                        {
                            SelectedTab = Tabs[importedSettings.SelectedTabIndex];
                        }
                        else
                        {
                            SelectedTab = Tabs.FirstOrDefault(t => !t.IsAddButton) ?? Tabs[0];
                        }
                    }

                    UpdateServiceStocks();
                    SaveStocks();
                    CalculatePortfolioTotals();
                    ApplySortInternal();

                    // Show modal dialog for the async update process
                    var progressDialog = new ConfirmationWindow("Importing", "Importing and analyzing...", isAlert: true)
                    {
                        AutoRunTask = async () =>
                        {
                            StatusMessage = "Refreshing imported data...";
                            await _stockService.UpdateAllDataAsync(SelectedRange);
                            
                            // Recalculate totals and graph after fresh data is loaded
                            CalculatePortfolioTotals();
                            ApplySortInternal();
                            
                            StatusMessage = "Import complete.";
                        },
                        SuccessMessage = "Import successful!"
                    };
                    
                    progressDialog.ShowDialog();
                }
                catch (Exception ex)
                {
                    var alert = new ConfirmationWindow("Error", $"Import failed: {ex.Message}", isAlert: true);
                    alert.ShowDialog();
                }
            }
        }

        [RelayCommand]
        private void AddTab()
        {
            // Close any existing edit sessions
            foreach (var t in Tabs) t.IsEditing = false;

            // Generate unique name "Portfolio X"
            int counter = 1;
            string newName;
            do
            {
                newName = $"Portfolio {counter}";
                counter++;
            } while (Tabs.Any(t => t.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)));

            var newTab = new PortfolioTabViewModel(new PortfolioTab { Name = newName });
            newTab.PropertyChanged += Tab_PropertyChanged;
            newTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
            newTab.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
            
            // Insert before the Add Button (last index)
            if (Tabs.Count > 0)
            {
                Tabs.Insert(Tabs.Count - 1, newTab);
            }
            else
            {
                Tabs.Add(newTab);
            }
            
            SelectedTab = newTab;
            newTab.IsEditing = true; // Enable editing mode immediately
            NewSymbol = "Dow Jones";
            RequestSearchFocus?.Invoke(this, EventArgs.Empty);
            SaveStocks();
        }

        [RelayCommand]
        private void RemoveTab(PortfolioTabViewModel tab)
        {
            if (tab.IsAddButton) return;

            // Count actual tabs (excluding add button)
            var actualTabsCount = Tabs.Count(t => !t.IsAddButton);
            if (actualTabsCount <= 1)
            {
                StatusMessage = "Cannot remove the last tab.";
                return;
            }

            bool shouldRemove = true;
            if (tab.Stocks.Count > 0)
            {
                var confirm = new ConfirmationWindow("Remove Tab", $"Are you sure you want to remove '{tab.Name}'?", isAlert: false);
                shouldRemove = confirm.ShowDialog() == true;
            }

            if (shouldRemove)
            {
                // If the tab to remove is currently selected, select another one FIRST
                if (SelectedTab == tab)
                {
                    int indexToRemove = Tabs.IndexOf(tab);
                    PortfolioTabViewModel? newSelectedTab = null;

                    // Try to select the previous tab
                    if (indexToRemove > 0)
                    {
                        newSelectedTab = Tabs[indexToRemove - 1];
                    }
                    // Or the next one (which is now at the same index, but we haven't removed yet, so index+1)
                    else if (indexToRemove + 1 < Tabs.Count)
                    {
                        newSelectedTab = Tabs[indexToRemove + 1];
                    }

                    // Ensure we don't select the Add Button if possible
                    if (newSelectedTab != null && newSelectedTab.IsAddButton)
                    {
                        // If we picked the add button, try to find the first non-add button
                        newSelectedTab = Tabs.FirstOrDefault(t => t != tab && !t.IsAddButton);
                    }

                    if (newSelectedTab != null)
                    {
                        SelectedTab = newSelectedTab;
                    }
                }

                Tabs.Remove(tab);
                UpdateServiceStocks();
                SaveStocks();
            }
        }

        [RelayCommand]
        private void RenameTab(PortfolioTabViewModel tab)
        {
            if (tab.IsAddButton) return;

            // If we are already editing, just save
            if (tab.IsEditing)
            {
                tab.IsEditing = false;
                SaveStocks();
                return;
            }

            // Close any other edit sessions
            foreach (var t in Tabs) 
            {
                if (t != tab) t.IsEditing = false;
            }

            // Otherwise, enable editing mode
            tab.IsEditing = true;
        }

        public void MoveTab(PortfolioTabViewModel tab, int newIndex)
        {
            if (tab == null || tab.IsAddButton) return;

            int oldIndex = Tabs.IndexOf(tab);
            if (oldIndex < 0) return;

            // Ensure we don't move past the Add Button
            // The Add Button should always be at Tabs.Count - 1
            int addButtonIndex = Tabs.Count - 1;
            if (newIndex >= addButtonIndex) newIndex = addButtonIndex - 1;
            if (newIndex < 0) newIndex = 0;

            if (oldIndex != newIndex)
            {
                Tabs.Move(oldIndex, newIndex);
                SaveStocks();
            }
        }

        private void CalculatePortfolioTotals()
        {
            // If in Merged View, regenerate the merged list to reflect any price/share changes
            // This is a bit expensive but ensures accuracy. 
            // Optimization: Only do this if triggered by data update, not just selection change?
            // For now, safety first.
            if (IsMergedView)
            {
                GenerateMergedView();
            }

            if (!IsPortfolioMode) return;

            decimal totalValue = 0;
            decimal totalDayChangeValue = 0;
            
            var portfolioHistory = new System.Collections.Generic.List<double>();
            var portfolioTimestamps = new System.Collections.Generic.List<DateTime>();
            int maxHistoryCount = 0;
            double maxDayProgress = 0;
            Stock? stockWithMaxHistory = null;

            var includedStocks = Tabs.Where(t => !t.IsAddButton && t.IsIncludedInTotal)
                                     .SelectMany(t => t.Stocks)
                                     .ToList();

            foreach (var stock in includedStocks)
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
            foreach (var stock in includedStocks)
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
                    foreach (var stock in includedStocks)
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
            foreach (var stock in includedStocks)
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

            // Calculate tab percentages
            foreach (var tab in Tabs.Where(t => !t.IsAddButton))
            {
                if (tab.IsIncludedInTotal && totalValue > 0)
                {
                    decimal tabValue = tab.Stocks.Sum(s => s.MarketValue);
                    tab.PortfolioPercentage = (double)(tabValue / totalValue);
                }
                else
                {
                    tab.PortfolioPercentage = 0;
                }
            }

            // Calculate Aggregate Tax Allocations
            var taxAllocations = new Dictionary<TaxStatusType, double>();
            decimal totalIncludedValue = 0;

            foreach (var tab in Tabs.Where(t => !t.IsAddButton && t.IsIncludedInTotal))
            {
                decimal tabValue = tab.Stocks.Sum(s => s.MarketValue);
                totalIncludedValue += tabValue;

                foreach (var allocation in tab.TaxAllocations)
                {
                    if (allocation.Percentage > 0)
                    {
                        if (!taxAllocations.ContainsKey(allocation.Type))
                        {
                            taxAllocations[allocation.Type] = 0;
                        }
                        // Contribution = TabValue * (Allocation% / 100)
                        taxAllocations[allocation.Type] += (double)tabValue * (allocation.Percentage / 100.0);
                    }
                }
            }

            var newAggregateAllocations = new ObservableCollection<TaxAllocation>();
            if (totalIncludedValue > 0)
            {
                foreach (var kvp in taxAllocations)
                {
                    double percentage = (kvp.Value / (double)totalIncludedValue) * 100.0;
                    if (percentage > 0)
                    {
                        newAggregateAllocations.Add(new TaxAllocation 
                        { 
                            Type = kvp.Key, 
                            Percentage = percentage
                        });
                    }
                }
            }
            else
            {
                newAggregateAllocations.Add(new TaxAllocation 
                { 
                    Type = TaxStatusType.Unspecified, 
                    Percentage = 100
                });
            }
            AggregateTaxAllocations = newAggregateAllocations;
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
            if (SelectedTab == null && !IsMergedView) return;

            // Determine source list
            IList<Stock> sourceCollection = IsMergedView ? MergedStocks : SelectedTab!.Stocks;
            var sourceList = sourceCollection.ToList();
            
            System.Collections.Generic.List<Stock> sortedList;

            if (SortProperty == "DayChangeValue" || SortProperty == "MarketValue")
            {
                var withShares = sourceList.Where(s => s.Shares > 0);
                var withoutShares = sourceList.Where(s => s.Shares == 0);

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

                sortedList = sortedWithShares.Concat(sortedWithoutShares).ToList();
            }
            else
            {
                Func<Stock, object> keySelector = SortProperty switch
                {
                    "Name" => s => s.Name,
                    "Change" => s => s.ChangePercent,
                    _ => s => s.Symbol
                };

                sortedList = IsAscending
                    ? sourceList.OrderBy(keySelector).ToList()
                    : sourceList.OrderByDescending(keySelector).ToList();
            }

            // Update the collection in place
            sourceCollection.Clear();
            foreach (var s in sortedList)
            {
                sourceCollection.Add(s);
            }
            
            // Sync service (only if not merged view, as merged view is derived)
            if (!IsMergedView)
            {
                UpdateServiceStocks();
            }

            OnPropertyChanged(nameof(SymbolSortIcon));
            OnPropertyChanged(nameof(NameSortIcon));
            OnPropertyChanged(nameof(ChangeSortIcon));
            OnPropertyChanged(nameof(DayChangeValueSortIcon));
            OnPropertyChanged(nameof(MarketValueSortIcon));
        }

    }
}
