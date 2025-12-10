using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PortfolioWatch.Services
{
    public class GeminiService
    {
        private const string GeminiApiKey = "AIzaSyAgvj5TDE4h6CAhdyjqHEu0oQz-h5vCaxc";
        private const string GeminiProjectName = "projects/522850341164";
        private const string GeminiProjectNumber = "522850341164";
        private const string GeminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public async Task<string> AnalyzeScreenshotAsync(List<BitmapSource> images, string prompt)
        {
            var imageParts = new List<object>();
            
            // Add prompt first
            imageParts.Add(new { text = prompt });

            foreach (var img in images)
            {
                using (var ms = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(img));
                    encoder.Save(ms);
                    byte[] bytes = ms.ToArray();
                    string base64 = Convert.ToBase64String(bytes);
                    
                    imageParts.Add(new 
                    { 
                        inline_data = new 
                        { 
                            mime_type = "image/png", 
                            data = base64 
                        } 
                    });
                }
            }

            return await SendRequestAsync(imageParts);
        }

        private async Task<string> SendRequestAsync(List<object> parts)
        {
            var payload = new
            {
                contents = new[]
                {
                    new { parts = parts }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{GeminiApiUrl}?key={GeminiApiKey}", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API Error ({response.StatusCode}): {errorBody}");
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    if (doc.RootElement.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out JsonElement contentElem) && 
                            contentElem.TryGetProperty("parts", out JsonElement contentParts) && 
                            contentParts.GetArrayLength() > 0)
                        {
                            return contentParts[0].GetProperty("text").GetString() ?? string.Empty;
                        }
                    }
                }
            }
            return string.Empty;
        }
    }
}
