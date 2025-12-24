using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public class SettingsService
    {
        private readonly string _filePath;
        private AppSettings _currentSettings;

        private const string ObscuredKeyHex = "506F7274666F6C696F205761746368206973206E6F74207365637572652C20627574206974206973206174206C65617374207175697465204F4253435552452E20F09F988F";

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "PortfolioWatch");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "settings.json");
            _currentSettings = new AppSettings();
        }

        public AppSettings GetDefaultSettings()
        {
            var settings = new AppSettings();
            
            // Ensure default categories exist
            settings.TaxCategories.Add(new TaxCategory { Name = "Non-Taxable Roth", Type = TaxStatusType.NonTaxableRoth, ColorHex = "#228833" });
            settings.TaxCategories.Add(new TaxCategory { Name = "Taxable Pre-Tax IRA", Type = TaxStatusType.TaxablePreTaxIRA, ColorHex = "#EE6677" });
            settings.TaxCategories.Add(new TaxCategory { Name = "Taxable Capital Gains", Type = TaxStatusType.TaxableCapitalGains, ColorHex = "#0077BB" });

            // Ensure default tab
            settings.Tabs.Add(new PortfolioTab { Name = "Portfolio" });

            return settings;
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
                            if (_currentSettings.Tabs == null) _currentSettings.Tabs = new List<PortfolioTab>();
                            
                            _currentSettings.Tabs.Add(new PortfolioTab
                            {
                                Name = !string.IsNullOrWhiteSpace(_currentSettings.WindowTitle) ? _currentSettings.WindowTitle : "Portfolio",
                                Stocks = new List<Stock>(_currentSettings.Stocks)
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
            if (_currentSettings.Tabs == null) _currentSettings.Tabs = new List<PortfolioTab>();

            // Ensure TaxCategories collection exists
            if (_currentSettings.TaxCategories == null) _currentSettings.TaxCategories = new List<TaxCategory>();

            // Migration: Populate TaxCategories from existing allocations if empty
            if (_currentSettings.TaxCategories.Count == 0)
            {
                foreach (var tab in _currentSettings.Tabs)
                {
                    if (tab.TaxAllocations != null)
                    {
                        foreach (var alloc in tab.TaxAllocations)
                        {
                            if (alloc.Type == TaxStatusType.Unspecified) continue;

                            // Check if category exists
                            var existing = _currentSettings.TaxCategories.FirstOrDefault(c => c.Name == alloc.Name);
                            if (existing == null)
                            {
                                existing = new TaxCategory
                                {
                                    Name = alloc.Name,
                                    ColorHex = alloc.ColorHex,
                                    Type = alloc.Type
                                };
                                _currentSettings.TaxCategories.Add(existing);
                            }

                            // Link allocation to category
                            alloc.CategoryId = existing.Id;
                        }
                    }
                }

            }

            // Ensure default categories exist (restore if missing) - Run this ALWAYS, not just on migration
            EnsureDefaultCategory("Non-Taxable Roth", TaxStatusType.NonTaxableRoth, "#228833");
            EnsureDefaultCategory("Taxable Pre-Tax IRA", TaxStatusType.TaxablePreTaxIRA, "#EE6677");
            EnsureDefaultCategory("Taxable Capital Gains", TaxStatusType.TaxableCapitalGains, "#0077BB");

            // Sanitize: Remove null tabs and ensure non-null Stocks collections
            _currentSettings.Tabs.RemoveAll(t => t == null);
            foreach (var tab in _currentSettings.Tabs)
            {
                if (tab.Stocks == null) tab.Stocks = new List<Stock>();
                else tab.Stocks.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Symbol));
                
                if (tab.TaxAllocations == null) tab.TaxAllocations = new List<TaxAllocation>();
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

        public void ExportStocks(string filePath, IEnumerable<Stock>? stocksToExport = null, string? portfolioName = null, IEnumerable<TaxAllocation>? taxAllocations = null)
        {
            try
            {
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

                if (filePath.EndsWith(".pwatch", StringComparison.OrdinalIgnoreCase))
                {
                    var encryptedData = EncryptAndCompress(json);
                    File.WriteAllBytes(filePath, encryptedData);
                }
                else
                {
                    File.WriteAllText(filePath, json);
                }
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
                string json;

                if (filePath.EndsWith(".pwatch", StringComparison.OrdinalIgnoreCase))
                {
                    var encryptedBytes = File.ReadAllBytes(filePath);
                    json = DecryptAndDecompress(encryptedBytes);
                }
                else
                {
                    json = File.ReadAllText(filePath);
                }

                var importedSettings = new AppSettings();
                bool foundData = false;

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // Check for "Tabs"
                    if (doc.RootElement.TryGetProperty("Tabs", out var tabsElement))
                    {
                        var tabs = JsonSerializer.Deserialize<List<PortfolioTab>>(tabsElement.GetRawText());
                        if (tabs != null && tabs.Count > 0)
                        {
                            importedSettings.Tabs = tabs;
                            foundData = true;
                        }
                    }

                    // Check for "Stocks" (Legacy or Single Export)
                    if (doc.RootElement.TryGetProperty("Stocks", out var stocksElement))
                    {
                        var stocks = JsonSerializer.Deserialize<List<Stock>>(stocksElement.GetRawText());
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
                        var legacyStocks = JsonSerializer.Deserialize<List<Stock>>(json);
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
                    if (importedSettings.Tabs == null) importedSettings.Tabs = new List<PortfolioTab>();
                    importedSettings.Tabs.RemoveAll(t => t == null);
                    foreach (var tab in importedSettings.Tabs)
                    {
                        if (tab.Stocks == null) tab.Stocks = new List<Stock>();
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

        private byte[] EncryptAndCompress(string plainText)
        {
            byte[] compressedBytes;

            // 1. Compress
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
                using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
                {
                    writer.Write(plainText);
                }
                compressedBytes = outputStream.ToArray();
            }

            // 2. Encrypt
            using (var aes = Aes.Create())
            {
                var key = GetKey();
                aes.Key = key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var msEncrypt = new MemoryStream())
                {
                    // Prepend IV
                    msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(compressedBytes, 0, compressedBytes.Length);
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        private string DecryptAndDecompress(byte[] cipherText)
        {
            byte[] compressedBytes;

            // 1. Decrypt
            using (var aes = Aes.Create())
            {
                var key = GetKey();
                aes.Key = key;

                // Extract IV
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherText, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var msDecrypt = new MemoryStream(cipherText, iv.Length, cipherText.Length - iv.Length))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var msPlain = new MemoryStream())
                {
                    csDecrypt.CopyTo(msPlain);
                    compressedBytes = msPlain.ToArray();
                }
            }

            // 2. Decompress
            using (var inputStream = new MemoryStream(compressedBytes))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private byte[] GetKey()
        {
            // Decode hex string
            var bytes = new byte[ObscuredKeyHex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(ObscuredKeyHex.Substring(i * 2, 2), 16);
            }
            
            // Hash to get 32 bytes (256 bits) for AES-256
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        private void EnsureDefaultCategory(string name, TaxStatusType type, string color)
        {
            // Check by Type first (for strict defaults) or Name (if user renamed but kept type?)
            // Actually, we want to ensure these specific defaults exist.
            // If a category with the same Type exists, we assume it's the default (even if renamed).
            // If a category with the same Name exists, we assume it's the default.
            
            var existing = _currentSettings.TaxCategories.FirstOrDefault(c => c.Type == type);
            if (existing == null)
            {
                // Try by name
                existing = _currentSettings.TaxCategories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            if (existing == null)
            {
                _currentSettings.TaxCategories.Add(new TaxCategory 
                { 
                    Name = name, 
                    Type = type, 
                    ColorHex = color 
                });
            }
        }
    }
}
