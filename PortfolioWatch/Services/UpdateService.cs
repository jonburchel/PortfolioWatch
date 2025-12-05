using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioWatch.Services
{
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class UpdateInfo
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }

    public class UpdateService
    {
        private const string RepoOwner = "jonburchel";
        private const string RepoName = "PortfolioWatch";
        private const string UserAgent = "PortfolioWatch-App";

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
                    
                    var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                    var response = await client.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                        return new UpdateInfo { IsUpdateAvailable = false };

                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release == null || release.Assets == null)
                        return new UpdateInfo { IsUpdateAvailable = false };

                    // Get current binary hash
                    var currentProcess = Process.GetCurrentProcess();
                    var currentBinaryPath = currentProcess.MainModule.FileName;
                    var currentHash = ComputeSha256Hash(currentBinaryPath);
                    var currentSize = new FileInfo(currentBinaryPath).Length;

                    // Determine which asset to look for based on size
                    // < 100MB -> smaller file (likely self-contained false)
                    // > 100MB -> larger file (likely self-contained true)
                    bool preferLarger = currentSize > 100 * 1024 * 1024;

                    // Find the appropriate asset
                    // We expect 2 binary downloads. We'll sort by size.
                    var binaries = release.Assets
                        .Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(a => a.Size)
                        .ToList();

                    if (binaries.Count == 0)
                        return new UpdateInfo { IsUpdateAvailable = false };

                    GitHubAsset targetAsset;
                    if (binaries.Count >= 2)
                    {
                        targetAsset = preferLarger ? binaries.Last() : binaries.First();
                    }
                    else
                    {
                        targetAsset = binaries.First();
                    }

                    // Check if the hash matches
                    // We assume the hash is in the release body or we might have to download to check?
                    // The requirement says: "If our current SHA hash of the binary doesn't match the published SHA of one of the binaries in the latest release"
                    // This implies the SHA is published in the release body text.
                    // We'll look for the hash in the body.
                    
                    if (!IsHashInBody(release.Body, currentHash))
                    {
                        // If current hash is NOT in the body, it means we are different from the release, so update is available.
                        // Wait, if we are running the latest version, our hash SHOULD be in the body.
                        // So if IsHashInBody is false, then we have an update (or we are running a dev build).
                        
                        return new UpdateInfo
                        {
                            IsUpdateAvailable = true,
                            Version = release.TagName,
                            ReleaseDate = release.PublishedAt,
                            DownloadUrl = targetAsset.BrowserDownloadUrl,
                            FileName = targetAsset.Name
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }

            return new UpdateInfo { IsUpdateAvailable = false };
        }

        private string ComputeSha256Hash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private bool IsHashInBody(string body, string hash)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(hash))
                return false;
            
            return body.Contains(hash, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ApplyUpdateAsync(string downloadUrl, string fileName)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                
                // Download file
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
                    var data = await client.GetByteArrayAsync(downloadUrl);
                    File.WriteAllBytes(tempPath, data);
                }

                // Create update script
                var currentProcess = Process.GetCurrentProcess();
                var currentPath = currentProcess.MainModule.FileName;
                var currentDir = Path.GetDirectoryName(currentPath);
                var scriptPath = Path.Combine(Path.GetTempPath(), "update_portfolio_watch.bat");
                var pid = currentProcess.Id;

                var script = $@"
@echo off
timeout /t 2 /nobreak > NUL
:loop
tasklist /FI ""PID eq {pid}"" 2>NUL | find /I /N ""{pid}"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak > NUL
    goto loop
)
move /y ""{tempPath}"" ""{currentPath}""
start """" ""{currentPath}""
del ""%~f0""
";
                File.WriteAllText(scriptPath, script);

                // Run script and exit
                var psi = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                Process.Start(psi);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
