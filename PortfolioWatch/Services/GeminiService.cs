using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PortfolioWatch.Services
{
    public class GeminiService
    {
        // TODO: Update this URL if you redeploy to a different address
        private const string BaseUrl = "https://portfolio-watch-eghnhrb0fgd0gyb3.centralus-01.azurewebsites.net";

        public async Task<string> AnalyzeScreenshotAsync(List<BitmapSource> images)
        {
            var base64Images = new List<string>();

            foreach (var img in images)
            {
                using (var ms = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(ms);
                    byte[] bytes = ms.ToArray();
                    string base64 = Convert.ToBase64String(bytes);
                    base64Images.Add(base64);
                }
            }

            return await SendRequestAsync(base64Images);
        }

        public async Task<string> LookupSymbolAsync(string cusip, string companyName)
        {
            var payload = new
            {
                Cusip = cusip,
                CompanyName = companyName
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{BaseUrl}/lookup-symbol", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    return "Untrackable";
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    if (doc.RootElement.TryGetProperty("text", out JsonElement textElem))
                    {
                        return textElem.GetString() ?? "Untrackable";
                    }
                    if (doc.RootElement.TryGetProperty("Text", out JsonElement textElemUpper))
                    {
                        return textElemUpper.GetString() ?? "Untrackable";
                    }
                }
            }
            return "Untrackable";
        }

        private async Task<string> SendRequestAsync(List<string> images)
        {
            var payload = new
            {
                Images = images
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{BaseUrl}/analyze", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Service Error ({response.StatusCode}): {errorBody}");
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    if (doc.RootElement.TryGetProperty("text", out JsonElement textElem))
                    {
                        return textElem.GetString() ?? string.Empty;
                    }
                    // Fallback for case-insensitive property matching if needed, though System.Text.Json is case-sensitive by default
                    if (doc.RootElement.TryGetProperty("Text", out JsonElement textElemUpper))
                    {
                        return textElemUpper.GetString() ?? string.Empty;
                    }
                }
            }
            return string.Empty;
        }
    }
}
