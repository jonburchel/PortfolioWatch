using System.Collections.Generic;
using System.Windows;

namespace PortfolioWatch.Models
{
    public enum AppTheme
    {
        System,
        Light,
        Dark
    }

    public class PortfolioTab
    {
        public System.Guid Id { get; set; } = System.Guid.NewGuid();
        public string Name { get; set; } = "Portfolio";
        public bool IsIncludedInTotal { get; set; } = true;
        public List<Stock> Stocks { get; set; } = new List<Stock>();
        public List<TaxAllocation> TaxAllocations { get; set; } = new List<TaxAllocation>();
    }

    public class AppSettings
    {
        public List<PortfolioTab> Tabs { get; set; } = new List<PortfolioTab>();
        public List<Stock> Stocks { get; set; } = new List<Stock>(); // Kept for backward compatibility
        public double WindowLeft { get; set; } = -9;
        public double WindowTop { get; set; } = SystemParameters.WorkArea.Bottom + 34 - 800;
        public double WindowWidth { get; set; } = 600;
        public double WindowHeight { get; set; } = 800;
        public string WindowTitle { get; set; } = "Watchlist";
        public string SortColumn { get; set; } = "Symbol";
        public bool SortAscending { get; set; } = true;
        public bool IsFirstRun { get; set; } = true;
        public bool IsIndexesVisible { get; set; } = true;
        public bool IsPortfolioMode { get; set; } = false;
        public bool ShowFloatingWindowIntradayPercent { get; set; } = true;
        public bool ShowFloatingWindowTotalValue { get; set; } = false;
        public bool ShowFloatingWindowIntradayGraph { get; set; } = true;
        public bool StartWithWindows { get; set; } = true;
        public AppTheme Theme { get; set; } = AppTheme.System;
        public double WindowOpacity { get; set; } = 0.85;
        public double UIScale { get; set; } = 1.0;
        public string SelectedRange { get; set; } = "1d";
        public int SelectedTabIndex { get; set; } = 0;
        
        // Update Settings
        public bool IsUpdateCheckEnabled { get; set; } = true;
        public System.DateTime? UpdateSnoozedUntil { get; set; }
        public System.DateTime LastUpdateCheck { get; set; } = System.DateTime.MinValue;
    }
}
