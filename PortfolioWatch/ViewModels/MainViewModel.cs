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
using PortfolioWatch.Helpers;

namespace PortfolioWatch.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IStockService _stockService;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;
        private readonly GeminiService _geminiService;
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
        private bool _isTabSwitching;

        [ObservableProperty]
        private string _newSymbol = string.Empty;

        [ObservableProperty]
        private bool _isSearchPopupOpen;

        [ObservableProperty]
        private bool _isEditingShares;

        private bool _isSorting;

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
        private bool _hasPortfolioData;

        [ObservableProperty]
        private System.Collections.Generic.List<double> _portfolioHistory = new();

        [ObservableProperty]
        private System.Collections.Generic.List<DateTime> _portfolioTimestamps = new();

        [ObservableProperty]
        private double _portfolioDayProgress;

        // --- Intraday Portfolio Properties (Always 1D) ---

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IntradayPortfolioPreviousClose))]
        private decimal _intradayPortfolioValue;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IntradayPortfolioPreviousClose))]
        private decimal _intradayPortfolioChange;

        [ObservableProperty]
        private double _intradayPortfolioChangePercent;

        public double IntradayPortfolioPreviousClose => (double)(IntradayPortfolioValue - IntradayPortfolioChange);

        [ObservableProperty]
        private bool _isIntradayPortfolioUp;

        [ObservableProperty]
        private System.Collections.Generic.List<double> _intradayPortfolioHistory = new();

        [ObservableProperty]
        private System.Collections.Generic.List<DateTime> _intradayPortfolioTimestamps = new();

        [ObservableProperty]
        private double _intradayPortfolioDayProgress;

        // -------------------------------------------------

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

        [ObservableProperty]
        private bool _showFloatingWindowIntradayPercent = true;

        partial void OnShowFloatingWindowIntradayPercentChanged(bool value)
        {
            if (!_isLoading) SaveStocks();
        }

        [ObservableProperty]
        private bool _showFloatingWindowTotalValue = false;

        partial void OnShowFloatingWindowTotalValueChanged(bool value)
        {
            if (!_isLoading) SaveStocks();
        }

        [ObservableProperty]
        private bool _showFloatingWindowIntradayGraph = true;

        partial void OnShowFloatingWindowIntradayGraphChanged(bool value)
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

        public List<TaxCategory> TaxCategories => _settingsService.CurrentSettings.TaxCategories;

        public event EventHandler? RequestSearchFocus;
        public event EventHandler<PortfolioTabViewModel>? RequestTaxStatusEdit;
        public event EventHandler? RequestScrollToNewTab;
        public event EventHandler? RequestShowAndPin;

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

            // Replace collection instance to avoid VirtualizingStackPanel crashes with Clear()/Add()
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
            if (newValue != null)
            {
                if (newValue.IsAddButton)
                {
                    AddTab();
                    return;
                }

                _isTabSwitching = true;
                try
                {
                    // Uncheck all other tabs
                    foreach (var tab in Tabs)
                    {
                        if (tab != newValue && !tab.IsAddButton)
                        {
                            tab.IsIncludedInTotal = false;
                        }
                    }

                    // Always include the selected tab
                    if (!newValue.IsIncludedInTotal)
                    {
                        newValue.IsIncludedInTotal = true;
                    }
                }
                finally
                {
                    _isTabSwitching = false;
                }
                
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

                var searchResultsList = new System.Collections.Generic.List<StockSearchResult>();

                // Check for CUSIP
                if (CusipHelper.IsValidCusip(value))
                {
                    searchResultsList.Add(new StockSearchResult 
                    { 
                        Symbol = value.ToUpper(), 
                        Name = "Privately tracked CUSIP" 
                    });
                }

                if (searchResult.Success && searchResult.Data != null)
                {
                    foreach (var result in searchResult.Data)
                    {
                        searchResultsList.Add(new StockSearchResult { Symbol = result.Symbol, Name = result.Name });
                    }
                }
                else if (searchResultsList.Count == 0)
                {
                    StatusMessage = searchResult.ErrorMessage ?? "Search failed";
                    return;
                }

                SearchResults.Clear();
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
            _geminiService = new GeminiService();
            
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

            EnsureTaxAllocations(newTab);

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

        public void Initialize(AppSettings? preloadedSettings = null)
        {
            LoadData(preloadedSettings);
        }

        private async void LoadData(AppSettings? preloadedSettings)
        {
            try
            {
                // Yield execution to allow UI to render immediately
                await Dispatcher.Yield();

                _isLoading = true;
                IsBusy = true;
                StatusMessage = "Loading data...";
                
                var settings = preloadedSettings ?? _settingsService.LoadSettings();
            
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
                    EnsureTaxAllocations(tabVm);
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
                EnsureTaxAllocations(defaultTab);
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
            ShowFloatingWindowIntradayPercent = settings.ShowFloatingWindowIntradayPercent;
            ShowFloatingWindowTotalValue = settings.ShowFloatingWindowTotalValue;
            ShowFloatingWindowIntradayGraph = settings.ShowFloatingWindowIntradayGraph;

            // Apply sort
            ApplySortInternal();
            
            // Unblock UI immediately with cached data
            IsBusy = false;
            _isLoading = false;

            // Initial fetch - Update Prices in background
            // We don't await this to allow the UI to be responsive immediately
            _ = Task.Run(async () => 
            {
                try
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background update error: {ex}");
                }
            });

            // Check for updates on startup
            _ = Task.Run(async () => 
            {
                try
                {
                    await CheckForUpdates(isManual: false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Startup update check error: {ex}");
                }
            });
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
            if (_isSorting) return;

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
                if (_isTabSwitching) return;

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

        public void UpdateTabTaxAllocations(PortfolioTabViewModel sourceTab, IEnumerable<TaxAllocation> allocations)
        {
            // 1. Update the source tab with new percentages
            sourceTab.TaxAllocations = new ObservableCollection<TaxAllocation>(allocations);

            // 2. Sync definitions on ALL tabs (including source, to ensure structure matches global TaxCategories)
            // This handles renaming, adding, or removing categories globally.
            foreach (var tab in Tabs.Where(t => !t.IsAddButton))
            {
                EnsureTaxAllocations(tab);
            }

            SaveStocks();
            CalculatePortfolioTotals(); // Re-calculate aggregate
        }

        private void EnsureTaxAllocations(PortfolioTabViewModel tab)
        {
            // This method ensures the tab's allocations match the global TaxCategories
            var newAllocations = new List<TaxAllocation>();
            var oldAllocations = tab.TaxAllocations.ToList();
            var categories = TaxCategories;

            foreach (var category in categories)
            {
                // Try to find match in target
                // Match by CategoryId first
                var match = oldAllocations.FirstOrDefault(a => a.CategoryId == category.Id);
                
                // If not found, try to match by ID (legacy behavior where ID might have been used as CategoryId?)
                // Or match by Type/Name for migration
                if (match == null)
                {
                    if (category.Type != TaxStatusType.Custom)
                    {
                        match = oldAllocations.FirstOrDefault(a => a.Type == category.Type);
                    }
                    else
                    {
                        match = oldAllocations.FirstOrDefault(a => a.Name == category.Name);
                    }
                }

                if (match != null)
                {
                    // Update definition to match global category
                    match.CategoryId = category.Id;
                    match.Name = category.Name;
                    match.ColorHex = category.ColorHex;
                    match.Type = category.Type;
                    
                    newAllocations.Add(match);
                    oldAllocations.Remove(match);
                }
                else
                {
                    // New category from global list
                    var newAlloc = new TaxAllocation
                    {
                        Id = Guid.NewGuid(),
                        CategoryId = category.Id,
                        Type = category.Type,
                        Name = category.Name,
                        ColorHex = category.ColorHex,
                        Percentage = 0
                    };
                    newAllocations.Add(newAlloc);
                }
            }

            // Handle orphaned percentages from deleted/unmatched categories
            double orphanedPercentage = oldAllocations.Sum(a => a.Percentage);
            
            // If this is a fresh tab (all 0s or just created), ensure Unspecified is 100%
            if (newAllocations.All(a => a.Percentage == 0) && orphanedPercentage == 0)
            {
                var unspecified = newAllocations.FirstOrDefault(a => a.Type == TaxStatusType.Unspecified);
                if (unspecified != null) 
                {
                    unspecified.Percentage = 100;
                }
                else 
                {
                    // Create Unspecified if it doesn't exist
                    newAllocations.Add(new TaxAllocation 
                    { 
                        Type = TaxStatusType.Unspecified, 
                        Name = "Unspecified", 
                        ColorHex = "#808080", 
                        Percentage = 100 
                    });
                }
            }
            else if (orphanedPercentage > 0)
            {
                // Redistribute orphaned percentage to Unspecified
                var unspecified = newAllocations.FirstOrDefault(a => a.Type == TaxStatusType.Unspecified);
                if (unspecified != null)
                {
                    unspecified.Percentage += orphanedPercentage;
                }
                else
                {
                    // Create Unspecified if it doesn't exist
                    newAllocations.Add(new TaxAllocation 
                    { 
                        Type = TaxStatusType.Unspecified, 
                        Name = "Unspecified", 
                        ColorHex = "#808080", 
                        Percentage = orphanedPercentage 
                    });
                }
            }

            tab.TaxAllocations = new ObservableCollection<TaxAllocation>(newAllocations);
        }

        private void Tab_RequestEditTaxStatus(object? sender, EventArgs e)
        {
            if (sender is PortfolioTabViewModel tab)
            {
                RequestTaxStatusEdit?.Invoke(this, tab);
            }
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
            settings.ShowFloatingWindowIntradayPercent = ShowFloatingWindowIntradayPercent;
            settings.ShowFloatingWindowTotalValue = ShowFloatingWindowTotalValue;
            settings.ShowFloatingWindowIntradayGraph = ShowFloatingWindowIntradayGraph;
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
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await _stockService.UpdateAllDataAsync(SelectedRange);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Periodic update error: {ex}");
                    }
                });
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

        [RelayCommand(AllowConcurrentExecutions = true)]
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
            _ = Task.Run(async () => 
            {
                try
                {
                    await _stockService.UpdateEarningsAsync();
                    await _stockService.UpdateNewsAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Refresh auxiliary data error: {ex}");
                }
            });
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
                if (CusipHelper.IsValidCusip(result.Symbol))
                {
                    NewSymbol = result.Symbol;
                    _ = AddStock();
                }
                else
                {
                    AddStockInternal(result.Symbol, result.Name);
                }
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
            
            // Fetch chart data
            _ = _stockService.UpdatePricesAsync(SelectedRange);

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
                    // Check for CUSIP
                    if (CusipHelper.IsValidCusip(NewSymbol))
                    {
                        if (SelectedTab != null && SelectedTab.Stocks.Any(s => s.Symbol == NewSymbol))
                        {
                            StatusMessage = $"{NewSymbol} is already in the list.";
                            NewSymbol = string.Empty;
                            IsSearchPopupOpen = false;
                            return;
                        }

                        var importWindow = new CusipImportWindow(NewSymbol);
                        if (ShowDialog(importWindow) == true)
                        {
                            // User provided details
                            string name = importWindow.FundName;
                            double quantity = importWindow.Quantity;
                            decimal totalValue = importWindow.TotalValue;
                            
                        // Lookup tracking symbol
                        string? trackingSymbol = null;
                        
                        var progressDialog = new ConfirmationWindow("Searching", "Identifying the best public tracking fund...", isAlert: true);
                        
                        progressDialog.AutoRunTask = async () =>
                        {
                            trackingSymbol = await _geminiService.LookupSymbolAsync(NewSymbol, name);
                            
                            if (trackingSymbol != null && trackingSymbol != "Untrackable")
                                progressDialog.SuccessMessage = "Search complete. The CUSIP will be tracked through public fund " + trackingSymbol + ".";
                            else
                                progressDialog.SuccessMessage = "No suitable public tracking fund could be found for this private CUSIP. It is not trackable with Portfolio Watch.";
                        };
                        
                        ShowDialog(progressDialog);
                        
                        if (trackingSymbol == "Untrackable") trackingSymbol = null;

                        var stock = new Stock
                            {
                                Symbol = NewSymbol,
                                Name = name,
                                Shares = quantity,
                                IsCusip = true,
                                TrackingFundSymbol = trackingSymbol ?? string.Empty
                            };

                            if (!string.IsNullOrEmpty(trackingSymbol))
                            {
                                // Fetch tracking fund price
                                var quotes = await _stockService.GetQuotesAsync(new[] { trackingSymbol });
                                if (quotes.Success && quotes.Data != null && quotes.Data.Count > 0)
                                {
                                    var quote = quotes.Data[0];
                                    if (quote.Price > 0)
                                    {
                                        // Calculate TrackingShares
                                        // TotalValue = TrackingShares * Price
                                        // TrackingShares = TotalValue / Price
                                        stock.TrackingShares = (double)(totalValue / (decimal)quote.Price.Value);
                                        
                                        // Calculate Conversion Ratio
                                        // Ratio = TrackingShares / UserShares
                                        if (quantity > 0)
                                        {
                                            stock.CusipConversionRatio = stock.TrackingShares / quantity;
                                        }

                                        stock.TrackingFundName = quote.Name;
                                        
                                        // Set initial price for display (it will be updated by service later)
                                        stock.Price = (decimal)(quote.Price ?? 0);
                                        stock.Change = (decimal)(quote.Change ?? 0);
                                        stock.ChangePercent = quote.ChangePercent ?? 0;
                                    }
                                }
                            }
                            else
                            {
                                // Untrackable: Just set price manually based on value?
                                // Price = TotalValue / Quantity
                                if (quantity > 0)
                                {
                                    stock.Price = totalValue / (decimal)quantity;
                                }
                            }

                            // Add to list
                            if (SelectedTab != null && !SelectedTab.IsAddButton)
                            {
                                SelectedTab.Stocks.Add(stock);
                                stock.PropertyChanged += Stock_PropertyChanged;
                                UpdateServiceStocks();
                                SaveStocks();
                                CalculatePortfolioTotals();
                                ApplySortInternal();
                            }
                            
                            NewSymbol = string.Empty;
                            IsSearchPopupOpen = false;
                            StatusMessage = "Fund added.";

                            // Trigger background update to fetch chart data immediately
                            _ = _stockService.UpdatePricesAsync(SelectedRange);
                            _ = _stockService.UpdateAuxiliaryDataAsync();
                        }
                    }
                    else
                    {
                        StatusMessage = result.ErrorMessage ?? "Failed to find stock details";
                    }
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
            
            if (ShowDialog(confirmationWindow) != true) return;

            if (confirmationWindow.ResetSettings)
            {
                // Clear settings
                var settings = _settingsService.GetDefaultSettings();
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
            ShowDialog(alert);

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
                
                if (ShowDialog(inputWindow) == true && !string.IsNullOrWhiteSpace(inputWindow.InputText))
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
        private void SavePortfolio()
        {
            SaveStocks(); // Ensure latest changes (like tab renames) are persisted before export

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "PortfolioWatch_Data",
                DefaultExt = ".pwatch",
                Filter = "Portfolio Watch Files (*.pwatch)|*.pwatch|JSON Files (*.json)|*.json"
            };

            if (ShowFileDialog(dialog) == true)
            {
                try
                {
                    _settingsService.ExportStocks(dialog.FileName, portfolioName: WindowTitle);
                    var successDialog = new ConfirmationWindow("Success", "Saved successfully!", isAlert: true, icon: "✅");
                    ShowDialog(successDialog);
                }
                catch (Exception ex)
                {
                    var errorDialog = new ConfirmationWindow("Error", $"Save failed: {ex.Message}", isAlert: true, icon: "❌");
                    ShowDialog(errorDialog);
                }
            }
        }

        [RelayCommand]
        private void NormalizeCurrentTab()
        {
            SaveStocks(); // Ensure latest changes are persisted

            if (TotalPortfolioValue <= 0)
            {
                var errorDialog = new ConfirmationWindow("Error", "Cannot normalize an empty portfolio.", isAlert: true, icon: "⚠️");
                ShowDialog(errorDialog);
                return;
            }

            var inputWindow = new InputWindow(
                "Enter the target portfolio value for normalization:", 
                "Normalize Portfolio", 
                "1,000,000",
                input => 
                {
                    var clean = input.Replace("$", "").Replace(",", "");
                    return decimal.TryParse(clean, out decimal val) && val > 0 ? null : "Please enter a valid positive number.";
                },
                radioOption1: "Create a new tab",
                radioOption2: "Save to file");
            
            if (ShowDialog(inputWindow) == true)
            {
                var cleanAmount = inputWindow.InputText.Replace("$", "").Replace(",", "");
                if (decimal.TryParse(cleanAmount, out decimal targetValue))
                {
                    try
                    {
                        var normalizedStocks = new System.Collections.Generic.List<Stock>();
                        
                        // Use currently visible stocks (Merged or Single Tab)
                        var sourceStocks = IsMergedView ? MergedStocks : SelectedTab?.Stocks;
                        if (sourceStocks == null) return;

                        foreach (var stock in sourceStocks)
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

                                // If CUSIP, convert tracking shares back to user shares
                                if (stock.IsCusip && stock.CusipConversionRatio > 0)
                                {
                                    newShares = newShares / stock.CusipConversionRatio;
                                }
                            }

                            // Create a copy with normalized shares (or 0 for watchlist items)
                            var normalizedStock = stock.Clone();
                            normalizedStock.Shares = newShares;
                            normalizedStocks.Add(normalizedStock);
                        }

                        string originalName = IsMergedView ? "Merged View" : (SelectedTab?.Name ?? "Portfolio");
                        string dateStr = DateTime.Now.ToString("M-d-yy");
                        string newTabName = $"{originalName} (x̂ → {targetValue:C0} on {dateStr})";

                        if (inputWindow.IsCheckBoxChecked) // Export to File (IsCheckBoxChecked is true when RadioOption2 is selected)
                        {
                            var saveDialog = new Microsoft.Win32.SaveFileDialog
                            {
                                FileName = newTabName,
                                DefaultExt = ".pwatch",
                                Filter = "Portfolio Watch Files (*.pwatch)|*.pwatch|JSON Files (*.json)|*.json"
                            };

                            if (ShowFileDialog(saveDialog) == true)
                            {
                                _settingsService.ExportStocks(
                                    saveDialog.FileName, 
                                    stocksToExport: normalizedStocks, 
                                    portfolioName: newTabName,
                                    taxAllocations: IsMergedView ? AggregateTaxAllocations : SelectedTab?.TaxAllocations
                                );
                                
                                var successDialog = new ConfirmationWindow("Success", "Exported successfully!", isAlert: true, icon: "✅");
                                ShowDialog(successDialog);
                            }
                        }
                        else // Create Tab
                        {
                            // Ensure unique name
                            int counter = 1;
                            string uniqueName = newTabName;
                            while (Tabs.Any(t => t.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)))
                            {
                                uniqueName = $"{newTabName} {counter++}";
                            }

                            var newTab = new PortfolioTabViewModel(new PortfolioTab 
                            { 
                                Name = uniqueName,
                                Stocks = normalizedStocks,
                                // Copy tax allocations from current view
                                TaxAllocations = IsMergedView ? AggregateTaxAllocations.ToList() : SelectedTab?.TaxAllocations.ToList() ?? new List<TaxAllocation>()
                            });

                            EnsureTaxAllocations(newTab);

                            // Subscribe to events
                            newTab.PropertyChanged += Tab_PropertyChanged;
                            newTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
                            newTab.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
                            foreach (var stock in newTab.Stocks)
                            {
                                stock.PropertyChanged += Stock_PropertyChanged;
                            }

                            // Insert before Add Button
                            if (Tabs.Count > 0)
                            {
                                Tabs.Insert(Tabs.Count - 1, newTab);
                            }
                            else
                            {
                                Tabs.Add(newTab);
                            }

                            // Select and scroll to new tab
                            SelectedTab = newTab;
                            IsMergedView = false;
                            RequestScrollToNewTab?.Invoke(this, EventArgs.Empty);
                            RequestShowAndPin?.Invoke(this, EventArgs.Empty);
                            
                            SaveStocks();
                            CalculatePortfolioTotals();
                            
                            var successDialog = new ConfirmationWindow("Success", $"Created new tab '{uniqueName}'.", isAlert: true, icon: "✅");
                            ShowDialog(successDialog);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorDialog = new ConfirmationWindow("Error", $"Normalization failed: {ex.Message}", isAlert: true, icon: "❌");
                        ShowDialog(errorDialog);
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

                ShowDialog(prompt);

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
                    ShowDialog(prompt);
                }
            }
        }

        [RelayCommand]
        private async Task ImportFromScreenshot()
        {
            await Task.Yield(); // Ensure async execution context
            var importWindow = new ScreenshotImportWindow();
            
            // Local variables to hold results from the processing action
            var newTabs = new System.Collections.Generic.List<PortfolioTabViewModel>();
            var untrackableCusips = new System.Collections.Generic.List<ParsedHolding>();
            int tabsCreated = 0;

            importWindow.ProcessHoldingsAction = async (parsedHoldings) =>
            {
                // Group by AccountName
                var groupedHoldings = parsedHoldings.GroupBy(h => string.IsNullOrWhiteSpace(h.AccountName) ? $"Imported {DateTime.Now:g}" : h.AccountName);

                foreach (var group in groupedHoldings)
                {
                    var tabName = group.Key;
                    // Ensure unique name (check against existing Tabs AND newTabs being created)
                    int counter = 1;
                    string uniqueName = tabName;
                    while (Tabs.Any(t => t.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)) || 
                           newTabs.Any(t => t.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)))
                    {
                        uniqueName = $"{tabName} {counter++}";
                    }

                    var newTab = new PortfolioTabViewModel(new PortfolioTab 
                    { 
                        Name = uniqueName
                    });

                    EnsureTaxAllocations(newTab);

                    foreach (var item in group)
                    {
                        if (CusipHelper.IsValidCusip(item.Symbol))
                        {
                            string trackingSymbol = await _geminiService.LookupSymbolAsync(item.Symbol, item.Name);

                            if (trackingSymbol != null && trackingSymbol != "Untrackable")
                            {
                                var stock = new Stock
                                {
                                    Symbol = item.Symbol,
                                    Name = item.Name,
                                    Shares = item.Quantity,
                                    IsCusip = true,
                                    TrackingFundSymbol = trackingSymbol
                                };

                                // Fetch tracking fund price
                                var quotes = await _stockService.GetQuotesAsync(new[] { trackingSymbol });
                                if (quotes.Success && quotes.Data != null && quotes.Data.Count > 0)
                                {
                                    var quote = quotes.Data[0];
                                    if (quote.Price > 0)
                                    {
                                        stock.TrackingShares = (double)(item.Value / quote.Price.Value);
                                        if (item.Quantity > 0)
                                        {
                                            stock.CusipConversionRatio = stock.TrackingShares / item.Quantity;
                                        }
                                        stock.TrackingFundName = quote.Name;
                                        stock.Price = (decimal)(quote.Price ?? 0);
                                        stock.Change = (decimal)(quote.Change ?? 0);
                                        stock.ChangePercent = quote.ChangePercent ?? 0;
                                    }
                                }
                                newTab.Stocks.Add(stock);
                            }
                            else
                            {
                                untrackableCusips.Add(item);
                            }
                        }
                        else
                        {
                            var stock = new Stock
                            {
                                Symbol = item.Symbol,
                                Name = item.Name,
                                Shares = item.Quantity
                            };
                            newTab.Stocks.Add(stock);
                        }
                    }
                    
                    // Batch fetch prices for regular stocks in this tab
                    var regularSymbols = newTab.Stocks.Where(s => !s.IsCusip).Select(s => s.Symbol).ToList();
                    if (regularSymbols.Any())
                    {
                         var quotes = await _stockService.GetQuotesAsync(regularSymbols);
                         if (quotes.Success && quotes.Data != null)
                         {
                             foreach(var quote in quotes.Data)
                             {
                                 var s = newTab.Stocks.FirstOrDefault(x => x.Symbol == quote.Symbol);
                                 if (s != null)
                                 {
                                     s.Price = (decimal)(quote.Price ?? 0);
                                     s.Change = (decimal)(quote.Change ?? 0);
                                     s.ChangePercent = quote.ChangePercent ?? 0;
                                 }
                             }
                         }
                    }

                    newTabs.Add(newTab);
                    tabsCreated++;
                }
            };
            
            if (ShowDialog(importWindow) == true && newTabs.Count > 0)
            {
                try
                {
                    StatusMessage = $"Importing {newTabs.Count} tabs...";
                    
                    PortfolioTabViewModel? firstNewTab = null;

                    foreach (var newTab in newTabs)
                    {
                        // Wire up events
                        newTab.PropertyChanged += Tab_PropertyChanged;
                        newTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
                        newTab.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
                        foreach (var stock in newTab.Stocks)
                        {
                            stock.PropertyChanged += Stock_PropertyChanged;
                        }

                        if (Tabs.Count > 0) Tabs.Insert(Tabs.Count - 1, newTab);
                        else Tabs.Add(newTab);

                        if (firstNewTab == null) firstNewTab = newTab;
                    }

                    if (firstNewTab != null)
                    {
                        SelectedTab = firstNewTab;
                        RequestScrollToNewTab?.Invoke(this, EventArgs.Empty);
                        RequestShowAndPin?.Invoke(this, EventArgs.Empty);
                    }

                    UpdateServiceStocks();
                    SaveStocks();
                    
                    // Trigger background update for history/aux data
                    _ = _stockService.UpdatePricesAsync(SelectedRange);
                    _ = _stockService.UpdateAuxiliaryDataAsync();
                    
                    StatusMessage = $"Imported {tabsCreated} tabs.";

                    if (untrackableCusips.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("The following CUSIPs could not be tracked and were excluded from import:");
                        sb.AppendLine();
                        foreach (var c in untrackableCusips)
                        {
                            sb.AppendLine($"- {c.Symbol} ({c.Name})");
                            sb.AppendLine($"  Shares: {c.Quantity}, Value: {c.Value:C}");
                            sb.AppendLine();
                        }
                        
                        var warningDialog = new ConfirmationWindow("Import Warning", sb.ToString(), isAlert: true, icon: "⚠️");
                        ShowDialog(warningDialog);
                    }
                }
                catch (Exception ex)
                {
                    var errorDialog = new ConfirmationWindow("Error", $"Import finalization failed: {ex.Message}", isAlert: true);
                    ShowDialog(errorDialog);
                }
            }
        }

        [RelayCommand]
        private void OpenPortfolio()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".pwatch",
                Filter = "Portfolio Watch Files (*.pwatch)|*.pwatch|JSON Files (*.json)|*.json"
            };

            if (ShowFileDialog(dialog) == true)
            {
                ImportPortfolio(dialog.FileName);
            }
        }

        public void ImportPortfolio(string filePath)
        {
            try
            {
                var importedSettings = _settingsService.ParseImportFile(filePath);
                if (importedSettings == null || importedSettings.Tabs.Count == 0)
                {
                    throw new Exception("No valid portfolio data found in file.");
                }

                var prompt = new ImportPromptWindow();
                
                if (ShowDialog(prompt) != true || prompt.Result == ImportAction.Cancel)
                {
                    return;
                }

                // Show modal dialog for the async update process
                var progressDialog = new ConfirmationWindow("Opening", "Opening portfolio...", isAlert: true)
                {
                    AutoRunTask = async () =>
                    {
                        StatusMessage = "Preparing portfolio...";
                        
                        // Build new tabs list locally
                        var newTabs = new System.Collections.Generic.List<PortfolioTabViewModel>();
                        PortfolioTabViewModel? firstImportedTab = null;

                        foreach (var tab in importedSettings.Tabs)
                        {
                            // Merge Tax Categories
                            if (tab.TaxAllocations != null)
                            {
                                foreach (var alloc in tab.TaxAllocations)
                                {
                                    if (alloc.Type == TaxStatusType.Unspecified) continue;

                                    // Check if category exists in global list
                                    // Match by ID first, then Name
                                    var existing = TaxCategories.FirstOrDefault(c => c.Id == alloc.CategoryId);
                                    if (existing == null)
                                    {
                                        existing = TaxCategories.FirstOrDefault(c => c.Name == alloc.Name);
                                    }

                                    if (existing == null)
                                    {
                                        // New category found in import
                                        existing = new TaxCategory
                                        {
                                            Id = alloc.CategoryId != Guid.Empty ? alloc.CategoryId : Guid.NewGuid(),
                                            Name = alloc.Name,
                                            ColorHex = alloc.ColorHex,
                                            Type = alloc.Type
                                        };
                                        TaxCategories.Add(existing);
                                    }

                                    // Link allocation to the global category (ensure ID matches)
                                    alloc.CategoryId = existing.Id;
                                    // Update properties to match global (in case of name match but different color/ID)
                                    alloc.Name = existing.Name;
                                    alloc.ColorHex = existing.ColorHex;
                                    alloc.Type = existing.Type;
                                }
                            }

                            var tabVm = new PortfolioTabViewModel(tab);
                            EnsureTaxAllocations(tabVm);
                            tabVm.IsIncludedInTotal = false; // Default to unchecked

                            if (firstImportedTab == null) firstImportedTab = tabVm;

                            // Wire up events (can do this here, but adding to Tabs collection must be on UI thread)
                            tabVm.PropertyChanged += Tab_PropertyChanged;
                            tabVm.Stocks.CollectionChanged += Stocks_CollectionChanged;
                            tabVm.RequestEditTaxStatus += Tab_RequestEditTaxStatus;
                            foreach (var stock in tabVm.Stocks)
                            {
                                stock.PropertyChanged += Stock_PropertyChanged;
                            }
                            
                            newTabs.Add(tabVm);
                        }

                        StatusMessage = "Fetching current prices...";
                        
                        // Fast fetch: Current Quotes only
                        var allSymbols = newTabs.SelectMany(t => t.Stocks)
                                             .Select(s => s.Symbol)
                                             .Distinct()
                                             .ToList();

                        if (allSymbols.Any())
                        {
                            var quotesResult = await _stockService.GetQuotesAsync(allSymbols);
                            
                            if (quotesResult.Success && quotesResult.Data != null)
                            {
                                foreach (var quote in quotesResult.Data)
                                {
                                    foreach (var tab in newTabs)
                                    {
                                        var stocksToUpdate = tab.Stocks.Where(s => s.Symbol == quote.Symbol);
                                        foreach (var stock in stocksToUpdate)
                                        {
                                            stock.Price = (decimal)(quote.Price ?? 0);
                                            stock.Change = (decimal)(quote.Change ?? 0);
                                            stock.ChangePercent = quote.ChangePercent ?? 0;
                                        }
                                    }
                                }
                            }
                        }

                        // Update UI on Dispatcher
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (prompt.Result == ImportAction.Replace)
                            {
                                Tabs.Clear();
                                WindowTitle = importedSettings.WindowTitle;
                            }
                            
                            // Add new tabs
                            foreach (var tab in newTabs)
                            {
                                if (Tabs.Count > 0 && Tabs.Last().IsAddButton)
                                {
                                    Tabs.Insert(Tabs.Count - 1, tab);
                                }
                                else
                                {
                                    Tabs.Add(tab);
                                }
                            }

                            // Ensure Add Button exists
                            if (!Tabs.Any(t => t.IsAddButton))
                            {
                                Tabs.Add(new PortfolioTabViewModel(true));
                            }

                            // Sync tax allocations for ALL tabs (existing + new) to ensure they match the merged categories
                            foreach (var tab in Tabs.Where(t => !t.IsAddButton))
                            {
                                EnsureTaxAllocations(tab);
                            }

                            IsMergedView = false;

                            if (firstImportedTab != null)
                            {
                                SelectedTab = firstImportedTab;
                                RequestScrollToNewTab?.Invoke(this, EventArgs.Empty);
                                RequestShowAndPin?.Invoke(this, EventArgs.Empty);
                            }
                            else if (Tabs.Count > 0)
                            {
                                SelectedTab = Tabs.FirstOrDefault(t => !t.IsAddButton) ?? Tabs[0];
                            }

                            UpdateServiceStocks();
                            SaveStocks();
                            CalculatePortfolioTotals();
                            ApplySortInternal();
                        });
                        
                        StatusMessage = "Open complete.";
                    },
                    SuccessMessage = "Open successful!"
                };
                
                ShowDialog(progressDialog);

                // Run heavy updates (History/Graphs/Auxiliary) in background after dialog closes
                _ = Task.Run(async () => 
                {
                    await _stockService.UpdatePricesAsync(SelectedRange);
                    await _stockService.UpdateAuxiliaryDataAsync();
                });
            }
            catch (Exception ex)
            {
                var alert = new ConfirmationWindow("Error", $"Open failed: {ex.Message}", isAlert: true);
                ShowDialog(alert);
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
            
            EnsureTaxAllocations(newTab);

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
            RequestScrollToNewTab?.Invoke(this, EventArgs.Empty);
            RequestShowAndPin?.Invoke(this, EventArgs.Empty);
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
                shouldRemove = ShowDialog(confirm) == true;
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

        public void MoveStockToTab(Stock stock, PortfolioTabViewModel targetTab)
        {
            if (stock == null || targetTab == null || targetTab.IsAddButton) return;
            if (SelectedTab == null || SelectedTab.IsAddButton) return;
            if (SelectedTab == targetTab) return;

            // Remove from current tab
            SelectedTab.Stocks.Remove(stock);

            // Add to target tab
            targetTab.Stocks.Add(stock);
            
            UpdateServiceStocks();
            SaveStocks();
            CalculatePortfolioTotals();
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
            
            // Intraday totals
            decimal totalIntradayChangeValue = 0;
            var intradayPortfolioHistory = new System.Collections.Generic.List<double>();
            var intradayPortfolioTimestamps = new System.Collections.Generic.List<DateTime>();
            double maxIntradayDayProgress = 0;

            var portfolioHistory = new System.Collections.Generic.List<double>();
            var portfolioTimestamps = new System.Collections.Generic.List<DateTime>();
            int maxHistoryCount = 0;
            double maxDayProgress = 0;
            Stock? stockWithMaxHistory = null;

            // Use MergedStocks when in merged view to avoid counting duplicates
            var includedStocks = IsMergedView 
                ? MergedStocks.ToList()
                : Tabs.Where(t => !t.IsAddButton && t.IsIncludedInTotal)
                      .SelectMany(t => t.Stocks)
                      .ToList();

            HasPortfolioData = includedStocks.Any(s => s.Shares > 0);

            foreach (var stock in includedStocks)
            {
                totalValue += stock.MarketValue;
                totalDayChangeValue += stock.DayChangeValue;
                
                // Intraday accumulation
                totalIntradayChangeValue += stock.IntradayChangeValue;

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

            // Aggregate history (Standard)
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

                            double shares = stock.IsCusip ? stock.TrackingShares : stock.Shares;
                            pointValue += priceAtTime * shares;
                        }
                    }
                    portfolioHistory.Add(pointValue);
                }
            }

            // Aggregate history (Intraday)
            var allIntradayTimestamps = new System.Collections.Generic.HashSet<DateTime>();
            foreach (var stock in includedStocks)
            {
                if (stock.Shares > 0 && stock.IntradayTimestamps != null)
                {
                    foreach (var ts in stock.IntradayTimestamps)
                    {
                        allIntradayTimestamps.Add(ts);
                    }
                }
            }

            if (allIntradayTimestamps.Count > 0)
            {
                intradayPortfolioTimestamps = allIntradayTimestamps.OrderBy(t => t).ToList();
                
                // Calculate Intraday Progress
                var lastTime = intradayPortfolioTimestamps.Last();
                // Assuming US market hours 9:30 to 16:00 local time
                var marketOpen = lastTime.Date.AddHours(9).AddMinutes(30);
                var marketClose = lastTime.Date.AddHours(16);
                
                if (lastTime >= marketClose)
                    maxIntradayDayProgress = 1.0;
                else if (lastTime <= marketOpen)
                    maxIntradayDayProgress = 0.0;
                else
                    maxIntradayDayProgress = (double)(lastTime - marketOpen).Ticks / (marketClose - marketOpen).Ticks;

                foreach (var ts in intradayPortfolioTimestamps)
                {
                    double pointValue = 0;
                    foreach (var stock in includedStocks)
                    {
                        if (stock.Shares > 0)
                        {
                            double priceAtTime = 0;
                            
                            if (stock.IntradayTimestamps != null && stock.IntradayHistory != null && stock.IntradayTimestamps.Count > 0)
                            {
                                int index = stock.IntradayTimestamps.FindLastIndex(t => t <= ts);
                                
                                if (index >= 0 && index < stock.IntradayHistory.Count)
                                {
                                    priceAtTime = stock.IntradayHistory[index];
                                }
                                else if (stock.IntradayHistory.Count > 0)
                                {
                                    priceAtTime = stock.IntradayHistory[0];
                                }
                            }
                            
                            if (priceAtTime == 0)
                            {
                                priceAtTime = (double)stock.Price;
                            }

                            double shares = stock.IsCusip ? stock.TrackingShares : stock.Shares;
                            pointValue += priceAtTime * shares;
                        }
                    }
                    intradayPortfolioHistory.Add(pointValue);
                }
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
                TotalPortfolioChangePercent = (double)(totalDayChangeValue / previousTotalValue);
            }
            else
            {
                TotalPortfolioChangePercent = 0;
            }

            IsPortfolioUp = TotalPortfolioChange >= 0;

            // Set Intraday Properties
            IntradayPortfolioValue = totalValue;
            IntradayPortfolioChange = totalIntradayChangeValue;
            IntradayPortfolioHistory = intradayPortfolioHistory;
            IntradayPortfolioTimestamps = intradayPortfolioTimestamps;
            IntradayPortfolioDayProgress = maxIntradayDayProgress;

            decimal previousIntradayValue = totalValue - totalIntradayChangeValue;
            if (previousIntradayValue != 0)
            {
                IntradayPortfolioChangePercent = (double)(totalIntradayChangeValue / previousIntradayValue);
            }
            else
            {
                IntradayPortfolioChangePercent = 0;
            }
            IsIntradayPortfolioUp = IntradayPortfolioChange >= 0;

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
                decimal tabValue = tab.Stocks.Sum(s => s.MarketValue);
                tab.TotalValue = tabValue;

                // Update Tax Allocation Values for this tab
                foreach (var allocation in tab.TaxAllocations)
                {
                    allocation.Value = (double)tabValue * (allocation.Percentage / 100.0);
                }

                if (tab.IsIncludedInTotal && totalValue > 0)
                {
                    tab.PortfolioPercentage = (double)(tabValue / totalValue);
                }
                else
                {
                    tab.PortfolioPercentage = 0;
                }
            }

            // Calculate Aggregate Tax Allocations
            var categoryTotals = new Dictionary<Guid, double>();
            decimal totalIncludedValue = 0;

            foreach (var tab in Tabs.Where(t => !t.IsAddButton && t.IsIncludedInTotal))
            {
                decimal tabValue = tab.Stocks.Sum(s => s.MarketValue);
                totalIncludedValue += tabValue;

                foreach (var allocation in tab.TaxAllocations)
                {
                    if (allocation.Percentage > 0)
                    {
                        var catId = allocation.CategoryId;
                        
                        // Fallback for legacy/unlinked allocations
                        if (catId == Guid.Empty)
                        {
                            // Try to find matching global category
                            var match = TaxCategories.FirstOrDefault(c => c.Name == allocation.Name || (c.Type == allocation.Type && c.Type != TaxStatusType.Custom));
                            if (match != null) catId = match.Id;
                        }

                        // If we have a valid ID (or found one), aggregate by it
                        if (catId != Guid.Empty)
                        {
                            if (!categoryTotals.ContainsKey(catId))
                            {
                                categoryTotals[catId] = 0;
                            }
                            categoryTotals[catId] += (double)tabValue * (allocation.Percentage / 100.0);
                        }
                        else
                        {
                            // If still no ID, group by Type as a last resort (legacy behavior)
                            // We'll use a temporary GUID based on Type to store in the dictionary
                            // This is a bit hacky but handles the edge case of unmigrated data
                            // However, since we can't easily map Type back to a Guid consistently without a lookup,
                            // we might lose color info if we don't look it up.
                            // Let's just skip unlinked custom categories or group them into "Unspecified"?
                            // Actually, let's try to find ANY category with that Type.
                            var match = TaxCategories.FirstOrDefault(c => c.Type == allocation.Type);
                            if (match != null)
                            {
                                catId = match.Id;
                                if (!categoryTotals.ContainsKey(catId)) categoryTotals[catId] = 0;
                                categoryTotals[catId] += (double)tabValue * (allocation.Percentage / 100.0);
                            }
                        }
                    }
                }
            }

            var newAggregateAllocations = new ObservableCollection<TaxAllocation>();
            if (totalIncludedValue > 0)
            {
                double totalAllocatedValue = 0;

                foreach (var kvp in categoryTotals)
                {
                    var catId = kvp.Key;
                    var totalVal = kvp.Value;
                    totalAllocatedValue += totalVal;

                    double percentage = (totalVal / (double)totalIncludedValue) * 100.0;
                    
                    if (percentage > 0)
                    {
                        // Look up category details
                        var category = TaxCategories.FirstOrDefault(c => c.Id == catId);
                        
                        string name = category?.Name ?? "Unknown";
                        string color = category?.ColorHex ?? "#888888";
                        TaxStatusType type = category?.Type ?? TaxStatusType.Custom;

                        newAggregateAllocations.Add(new TaxAllocation 
                        { 
                            Id = Guid.NewGuid(), // New ID for this aggregate view item
                            CategoryId = catId,
                            Type = type, 
                            Name = name,
                            ColorHex = color,
                            Percentage = percentage,
                            Value = totalVal
                        });
                    }
                }

                // Add Unspecified portion if any
                double unspecifiedValue = (double)totalIncludedValue - totalAllocatedValue;
                if (unspecifiedValue > 0.01)
                {
                    double percentage = (unspecifiedValue / (double)totalIncludedValue) * 100.0;
                    newAggregateAllocations.Add(new TaxAllocation 
                    { 
                        Type = TaxStatusType.Unspecified, 
                        Name = "Unspecified",
                        ColorHex = "#808080",
                        Percentage = percentage,
                        Value = unspecifiedValue
                    });
                }
            }
            else
            {
                newAggregateAllocations.Add(new TaxAllocation 
                { 
                    Type = TaxStatusType.Unspecified, 
                    Name = "Unspecified",
                    ColorHex = "#808080",
                    Percentage = 100,
                    Value = 0
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
            if (stock != null)
            {
                string symbolToOpen = stock.Symbol;
                bool useMorningstar = false;

                if (stock.IsCusip && !string.IsNullOrEmpty(stock.TrackingFundSymbol))
                {
                    symbolToOpen = stock.TrackingFundSymbol;
                    useMorningstar = true;
                }

                if (!string.IsNullOrWhiteSpace(symbolToOpen))
                {
                    try
                    {
                        var url = useMorningstar 
                            ? $"https://www.morningstar.com/funds/xnas/{symbolToOpen}/quote"
                            : $"https://finance.yahoo.com/quote/{symbolToOpen}";

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

        private bool? ShowDialog(System.Windows.Window dialog)
        {
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.GetType().Name == "MainWindow");

            PortfolioWatch.MainWindow? castWindow = mainWindow as PortfolioWatch.MainWindow;

            if (mainWindow != null && 
                mainWindow.IsVisible &&
                mainWindow.WindowState != System.Windows.WindowState.Minimized)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                
                if (castWindow != null) castWindow.CancelAutoHide();
            }
            else
            {
                dialog.Owner = null;
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                dialog.Topmost = true;
            }

            try
            {
                return dialog.ShowDialog();
            }
            finally
            {
                if (castWindow != null && 
                    mainWindow != null && 
                    mainWindow.IsVisible && 
                    mainWindow.WindowState != System.Windows.WindowState.Minimized)
                {
                    castWindow.StartAutoHide();
                }
            }
        }

        private bool? ShowFileDialog(Microsoft.Win32.CommonDialog dialog)
        {
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.GetType().Name == "MainWindow");

            PortfolioWatch.MainWindow? castWindow = mainWindow as PortfolioWatch.MainWindow;

            if (mainWindow != null && 
                mainWindow.IsVisible &&
                mainWindow.WindowState != System.Windows.WindowState.Minimized)
            {
                if (castWindow != null) castWindow.CancelAutoHide();
                try
                {
                    return dialog.ShowDialog(mainWindow);
                }
                finally
                {
                    if (castWindow != null) castWindow.StartAutoHide();
                }
            }
            else
            {
                return dialog.ShowDialog();
            }
        }

        private void ApplySortInternal()
        {
            if ((SelectedTab == null && !IsMergedView) || _isSorting || IsEditingShares) return;

            _isSorting = true;
            try
            {
                // Determine source list
                IList<Stock> sourceCollection = IsMergedView ? MergedStocks : SelectedTab!.Stocks;
                var sourceList = sourceCollection.Where(s => s != null).ToList();
                
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

                // Replace collection instance to avoid VirtualizingStackPanel crashes with Clear()/Add()
                var newCollection = new ObservableCollection<Stock>(sortedList);

                if (IsMergedView)
                {
                    MergedStocks = newCollection;
                    Stocks = MergedStocks;
                }
                else
                {
                    // Unsubscribe from old collection events
                    if (SelectedTab!.Stocks != null)
                    {
                        SelectedTab.Stocks.CollectionChanged -= Stocks_CollectionChanged;
                    }
                    
                    SelectedTab.Stocks = newCollection;
                    
                    // Re-subscribe
                    SelectedTab.Stocks.CollectionChanged += Stocks_CollectionChanged;
                    
                    // Update the bound property
                    Stocks = SelectedTab.Stocks;
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
            finally
            {
                _isSorting = false;
            }
        }

    }
}
