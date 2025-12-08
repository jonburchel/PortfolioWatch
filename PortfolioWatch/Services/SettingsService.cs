using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public class SettingsService
    {
        private readonly string _filePath;
        private AppSettings _currentSettings;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "PortfolioWatch");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "settings.json");
            _currentSettings = new AppSettings();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        
                        // Migration: If no tabs exist but we have stocks, create a default tab
                        if ((_currentSettings.Tabs == null || _currentSettings.Tabs.Count == 0) && 
                            _currentSettings.Stocks != null && _currentSettings.Stocks.Count > 0)
                        {
                            if (_currentSettings.Tabs == null) _currentSettings.Tabs = new System.Collections.Generic.List<PortfolioTab>();
                            
                            _currentSettings.Tabs.Add(new PortfolioTab
                            {
                                Name = !string.IsNullOrWhiteSpace(_currentSettings.WindowTitle) ? _currentSettings.WindowTitle : "Portfolio",
                                Stocks = new System.Collections.Generic.List<Stock>(_currentSettings.Stocks)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
                // Fall through to safety checks
            }

            // Ensure Tabs collection exists
            if (_currentSettings.Tabs == null) _currentSettings.Tabs = new System.Collections.Generic.List<PortfolioTab>();

            // Sanitize: Remove null tabs and ensure non-null Stocks collections
            _currentSettings.Tabs.RemoveAll(t => t == null);
            foreach (var tab in _currentSettings.Tabs)
            {
                if (tab.Stocks == null) tab.Stocks = new System.Collections.Generic.List<Stock>();
                else tab.Stocks.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Symbol));
            }

            // Also sanitize legacy Stocks list if present
            if (_currentSettings.Stocks != null)
            {
                _currentSettings.Stocks.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Symbol));
            }

            // Ensure at least one tab exists if everything is empty
            if (_currentSettings.Tabs.Count == 0)
            {
                _currentSettings.Tabs.Add(new PortfolioTab { Name = "Portfolio" });
            }

            return _currentSettings;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                _currentSettings = settings;
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
                // Ignore errors
            }
        }

        public AppSettings CurrentSettings => _currentSettings;

        public void SetStartup(bool enable)
        {
            try
            {
                string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        string? location = Process.GetCurrentProcess().MainModule?.FileName;
                        
                        if (!string.IsNullOrEmpty(location))
                        {
                            key.SetValue("PortfolioWatch", $"\"{location}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue("PortfolioWatch", false);
                    }
                }
                
                _currentSettings.StartWithWindows = enable;
                SaveSettings(_currentSettings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }

        public bool IsStartupEnabled()
        {
            try
            {
                string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue("PortfolioWatch") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public void ExportStocks(string filePath, System.Collections.Generic.IEnumerable<Stock>? stocksToExport = null, string? portfolioName = null, System.Collections.Generic.IEnumerable<TaxAllocation>? taxAllocations = null)
        {
            try
            {
                // If specific stocks are provided (e.g. normalized export), export just that list as a single "tab" structure or legacy structure
                // But the requirement says "include the tab details".
                
                object exportData;

                if (stocksToExport != null)
                {
                    // Exporting specific list (e.g. normalized) - treat as single portfolio
                    var stockList = stocksToExport.Select(stock => new
                    {
                        stock.Symbol,
                        stock.Name,
                        stock.Price,
                        stock.Change,
                        stock.ChangePercent,
                        stock.Shares
                    }).ToList();

                    // Use provided tax allocations or default to Unspecified 100%
                    var allocations = taxAllocations ?? new[] { new TaxAllocation { Type = TaxStatusType.Unspecified, Percentage = 100 } };

                    exportData = new
                    {
                        PortfolioName = portfolioName ?? "Exported Portfolio",
                        Stocks = stockList,
                        // Wrap in a Tabs structure for consistency if imported back
                        Tabs = new[] 
                        { 
                            new 
                            { 
                                Name = portfolioName ?? "Exported Portfolio", 
                                Stocks = stockList,
                                TaxAllocations = allocations
                            } 
                        }
                    };
                }
                else
                {
                    // Full export of current settings
                    var tabsExport = _currentSettings.Tabs.Select(t => new
                    {
                        t.Name,
                        t.IsIncludedInTotal,
                        t.TaxAllocations,
                        Stocks = t.Stocks.Select(stock => new
                        {
                            stock.Symbol,
                            stock.Name,
                            stock.Price,
                            stock.Change,
                            stock.ChangePercent,
                            stock.Shares
                        }).ToList()
                    }).ToList();

                    exportData = new
                    {
                        PortfolioName = _currentSettings.WindowTitle, // Main window title
                        _currentSettings.SortColumn,
                        _currentSettings.SortAscending,
                        _currentSettings.SelectedTabIndex,
                        _currentSettings.IsPortfolioMode,
                        _currentSettings.IsIndexesVisible,
                        _currentSettings.SelectedRange,
                        Tabs = tabsExport
                    };
                }

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export stocks: {ex.Message}");
            }
        }

        public AppSettings? ParseImportFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var importedSettings = new AppSettings();
                bool foundData = false;

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // Check for "Tabs"
                    if (doc.RootElement.TryGetProperty("Tabs", out var tabsElement))
                    {
                        var tabs = JsonSerializer.Deserialize<System.Collections.Generic.List<PortfolioTab>>(tabsElement.GetRawText());
                        if (tabs != null && tabs.Count > 0)
                        {
                            importedSettings.Tabs = tabs;
                            foundData = true;
                        }
                    }

                    // Check for "Stocks" (Legacy or Single Export)
                    if (doc.RootElement.TryGetProperty("Stocks", out var stocksElement))
                    {
                        var stocks = JsonSerializer.Deserialize<System.Collections.Generic.List<Stock>>(stocksElement.GetRawText());
                        if (stocks != null && stocks.Count > 0)
                        {
                            // If we already have tabs, we might ignore this or treat it as a fallback?
                            // If we don't have tabs, create a tab from this.
                            if (!foundData)
                            {
                                string name = "Imported";
                                if (doc.RootElement.TryGetProperty("PortfolioName", out var nameElement))
                                {
                                    name = nameElement.GetString() ?? "Imported";
                                }

                                importedSettings.Tabs.Add(new PortfolioTab
                                {
                                    Name = name,
                                    Stocks = stocks
                                });
                                foundData = true;
                            }
                        }
                    }
                    
                    // Check for "PortfolioName" for WindowTitle
                    if (doc.RootElement.TryGetProperty("PortfolioName", out var pNameElement))
                    {
                        importedSettings.WindowTitle = pNameElement.GetString() ?? "Watchlist";
                    }

                    // Import View Settings
                    if (doc.RootElement.TryGetProperty("SortColumn", out var sortCol))
                        importedSettings.SortColumn = sortCol.GetString() ?? "Symbol";
                    
                    if (doc.RootElement.TryGetProperty("SortAscending", out var sortAsc))
                        importedSettings.SortAscending = sortAsc.GetBoolean();

                    if (doc.RootElement.TryGetProperty("SelectedTabIndex", out var selTab))
                        importedSettings.SelectedTabIndex = selTab.GetInt32();

                    if (doc.RootElement.TryGetProperty("IsPortfolioMode", out var portMode))
                        importedSettings.IsPortfolioMode = portMode.GetBoolean();

                    if (doc.RootElement.TryGetProperty("IsIndexesVisible", out var idxVis))
                        importedSettings.IsIndexesVisible = idxVis.GetBoolean();

                    if (doc.RootElement.TryGetProperty("SelectedRange", out var selRange))
                        importedSettings.SelectedRange = selRange.GetString() ?? "1d";
                }

                // Fallback: Array of stocks (Legacy)
                if (!foundData)
                {
                    try
                    {
                        var legacyStocks = JsonSerializer.Deserialize<System.Collections.Generic.List<Stock>>(json);
                        if (legacyStocks != null && legacyStocks.Count > 0)
                        {
                            importedSettings.Tabs.Add(new PortfolioTab
                            {
                                Name = "Imported",
                                Stocks = legacyStocks
                            });
                            foundData = true;
                        }
                    }
                    catch { }
                }

                if (foundData)
                {
                    // Sanitize imported settings
                    if (importedSettings.Tabs == null) importedSettings.Tabs = new System.Collections.Generic.List<PortfolioTab>();
                    importedSettings.Tabs.RemoveAll(t => t == null);
                    foreach (var tab in importedSettings.Tabs)
                    {
                        if (tab.Stocks == null) tab.Stocks = new System.Collections.Generic.List<Stock>();
                        else tab.Stocks.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Symbol));
                    }
                    return importedSettings;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse import file: {ex.Message}");
            }
        }
    }
}
