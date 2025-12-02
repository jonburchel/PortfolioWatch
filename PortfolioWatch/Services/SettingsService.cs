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
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors and use defaults
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
            catch (Exception)
            {
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

        public void ExportStocks(string filePath)
        {
            try
            {
                // Export only details, not history
                var exportData = new System.Collections.Generic.List<object>();
                foreach (var stock in _currentSettings.Stocks)
                {
                    exportData.Add(new
                    {
                        stock.Symbol,
                        stock.Name,
                        stock.Price,
                        stock.Change,
                        stock.ChangePercent,
                        stock.Shares
                    });
                }

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export stocks: {ex.Message}");
            }
        }

        public void ImportStocks(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var stocks = JsonSerializer.Deserialize<System.Collections.Generic.List<Stock>>(json);
                if (stocks != null)
                {
                    _currentSettings.Stocks = stocks;
                    SaveSettings(_currentSettings);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import stocks: {ex.Message}");
            }
        }
    }
}
