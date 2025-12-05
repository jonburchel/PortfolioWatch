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

    public class AppSettings
    {
        public List<Stock> Stocks { get; set; } = new List<Stock>();
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double WindowWidth { get; set; } = 450;
        public double WindowHeight { get; set; } = 600;
        public string WindowTitle { get; set; } = "Watchlist";
        public string SortColumn { get; set; } = "Symbol";
        public bool SortAscending { get; set; } = true;
        public bool IsFirstRun { get; set; } = true;
        public bool IsIndexesVisible { get; set; } = true;
        public bool IsPortfolioMode { get; set; } = false;
        public bool StartWithWindows { get; set; } = true;
        public AppTheme Theme { get; set; } = AppTheme.System;
        public double WindowOpacity { get; set; } = 1.0;
        public string SelectedRange { get; set; } = "1d";
        
        // Update Settings
        public bool IsUpdateCheckEnabled { get; set; } = true;
        public System.DateTime? UpdateSnoozedUntil { get; set; }
        public System.DateTime LastUpdateCheck { get; set; } = System.DateTime.MinValue;
    }
}
