using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapPost("/analyze", async (AnalyzeRequest request, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        return Results.Problem("Server configuration error: API Key missing.");
    }

    if (request.Images == null || request.Images.Count == 0)
    {
        return Results.BadRequest("No images provided.");
    }

    var prompt = "Please analyze these screenshots of a portfolio. Extract the holdings into a markdown table with columns: Account name (blank if not found anywhere on the page, but if there is an account name above a group of securities, all of them should be listed with that account name even if not explicitly stated beside each security), Company name (use symbol if unknown), Symbol, Quantity, and Total Value. For cash positions, use 'SPAXX' as the symbol and 'Cash' as the company name, and set the share count equal to the dollar value. IMPORTANT: If any of the extracted Account Names appears as a holding in another account (e.g. a 'BrokerageLink' account appearing as a line item), EXCLUDE that holding from the table. Output ONLY the markdown table. If you cannot extract the data at all, output 'ERROR: <reason>'. Do not include any conversational text.";

    var imageParts = new List<object>();
    
    // Add prompt first
    imageParts.Add(new { text = prompt });

    foreach (var base64Image in request.Images)
    {
        imageParts.Add(new 
        { 
            inline_data = new 
            { 
                mime_type = "image/png", 
                data = base64Image 
            } 
        });
    }

    var payload = new
    {
        contents = new[]
        {
            new { parts = imageParts }
        }
    };

    var geminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    
    using var client = httpClientFactory.CreateClient();
    var jsonPayload = JsonSerializer.Serialize(payload);
    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
    
    try 
    {
        var response = await client.PostAsync($"{geminiApiUrl}?key={apiKey}", content);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            return Results.Problem($"Upstream API Error ({response.StatusCode}): {errorBody}");
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
                    var text = contentParts[0].GetProperty("text").GetString() ?? string.Empty;
                    return Results.Ok(new { Text = text });
                }
            }
        }
        
        return Results.Ok(new { Text = string.Empty });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Internal Server Error: {ex.Message}");
    }
});

app.MapPost("/lookup-symbol", async (SymbolLookupRequest request, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        return Results.Problem("Server configuration error: API Key missing.");
    }

    if (string.IsNullOrEmpty(request.Cusip) && string.IsNullOrEmpty(request.CompanyName))
    {
        return Results.BadRequest("CUSIP or Company Name must be provided.");
    }

    var prompt = $"I have a security with CUSIP '{request.Cusip}' and Company Name '{request.CompanyName}'. Please identify the best public fund ticker symbol to track this fund. If there is a direct ticker, return it. If not, return the best proxy ticker. If there is no suitable option that will track it closely or exactly, return 'Untrackable'. Return ONLY the ticker symbol or the word 'Untrackable'. Do not include any other text.";

    var payload = new
    {
        contents = new[]
        {
            new { parts = new[] { new { text = prompt } } }
        }
    };

    var geminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    
    using var client = httpClientFactory.CreateClient();
    var jsonPayload = JsonSerializer.Serialize(payload);
    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
    
    try 
    {
        var response = await client.PostAsync($"{geminiApiUrl}?key={apiKey}", content);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            return Results.Problem($"Upstream API Error ({response.StatusCode}): {errorBody}");
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
                    var text = contentParts[0].GetProperty("text").GetString() ?? string.Empty;
                    return Results.Ok(new { Text = text.Trim() });
                }
            }
        }
        
        return Results.Ok(new { Text = "Untrackable" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Internal Server Error: {ex.Message}");
    }
});

app.Run();

public class AnalyzeRequest
{
    public List<string> Images { get; set; } = new();
}

public class SymbolLookupRequest
{
    public string Cusip { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}
