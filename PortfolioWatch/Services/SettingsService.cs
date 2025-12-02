using System;
using System.IO;
using System.Text.Json;
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
    }
}
