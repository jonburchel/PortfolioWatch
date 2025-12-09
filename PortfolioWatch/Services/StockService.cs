using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using System.Net;
using System.IO;
using System.Windows.Media.Imaging;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public class StockService : IStockService
    {
        private readonly List<Stock> _stocks;
        private readonly List<Stock> _indexes;
        private readonly HttpClient _httpClient;
        private string _crumb = string.Empty;
        private bool _crumbAttempted = false;

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int delayMs = 1000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    System.Diagnostics.Debug.WriteLine($"Retry attempt {i + 1} failed: {ex.Message}");
                    await Task.Delay(delayMs * (i + 1));
                }
            }
            return await action();
        }

        public StockService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            _indexes = new List<Stock>
            {
                new Stock { Symbol = "^DJI", Name = "Dow Jones" },
                new Stock { Symbol = "^IXIC", Name = "Nasdaq" },
                new Stock { Symbol = "^GSPC", Name = "S&P 500" }
            };

            _stocks = GetDefaultStocks();
        }

        public List<Stock> GetDefaultStocks()
        {
            return new List<Stock>
            {
                new Stock { Symbol = "MSFT", Name = "Microsoft Corp" },
                new Stock { Symbol = "AAPL", Name = "Apple Inc" },
                new Stock { Symbol = "GOOGL", Name = "Alphabet Inc" },
                new Stock { Symbol = "AMZN", Name = "Amazon.com Inc" },
                new Stock { Symbol = "TSLA", Name = "Tesla Inc" },
                new Stock { Symbol = "NVDA", Name = "NVIDIA Corp" },
                new Stock { Symbol = "META", Name = "Meta Platforms" },
                new Stock { Symbol = "NFLX", Name = "Netflix Inc" },
                new Stock { Symbol = "AMD", Name = "Advanced Micro Devices" },
                new Stock { Symbol = "INTC", Name = "Intel Corp" }
            };
        }

        public Task<ServiceResult<List<Stock>>> GetStocksAsync()
        {
            return Task.FromResult(ServiceResult<List<Stock>>.Ok(_stocks));
        }

        public Task<ServiceResult<List<Stock>>> GetIndexesAsync()
        {
            return Task.FromResult(ServiceResult<List<Stock>>.Ok(_indexes));
        }

        public async Task<ServiceResult<List<(string Symbol, string Name)>>> SearchStocksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) 
                return ServiceResult<List<(string, string)>>.Ok(new List<(string, string)>());

            try
            {
                var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=10&newsCount=0";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                var quotes = doc.RootElement.GetProperty("quotes");
                
                var results = new List<(string Symbol, string Name)>();
                
                foreach (var quote in quotes.EnumerateArray())
                {
                    if (quote.TryGetProperty("symbol", out var symbolProp))
                    {
                        string symbol = symbolProp.GetString() ?? "";
                        string name = "";
                        
                        if (quote.TryGetProperty("shortname", out var nameProp))
                            name = nameProp.GetString() ?? "";
                        else if (quote.TryGetProperty("longname", out var longNameProp))
                            name = longNameProp.GetString() ?? "";
                            
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            results.Add((symbol, name ?? symbol));
                        }
                    }
                }
                
                return ServiceResult<List<(string, string)>>.Ok(results);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<(string, string)>>.Fail($"Search failed: {ex.Message}");
            }
        }

        public async Task<ServiceResult<(string Symbol, string Name)>> GetStockDetailsAsync(string query)
        {
            var searchResult = await SearchStocksAsync(query);
            if (!searchResult.Success)
            {
                return ServiceResult<(string, string)>.Fail(searchResult.ErrorMessage ?? "Unknown error");
            }

            var results = searchResult.Data;
            if (results == null || results.Count == 0)
            {
                 return ServiceResult<(string, string)>.Fail("Stock not found");
            }

            var match = results.FirstOrDefault(r => r.Symbol.Equals(query, StringComparison.OrdinalIgnoreCase));
            
            if (match.Symbol == null && results.Count > 0)
            {
                // If no exact match, take the first one
                match = results[0];
            }
            
            if (string.IsNullOrEmpty(match.Symbol))
            {
                 return ServiceResult<(string, string)>.Fail("Stock not found");
            }

            return ServiceResult<(string, string)>.Ok(match);
        }

        public async Task<ServiceResult<List<StockSearchResult>>> GetQuotesAsync(IEnumerable<string> symbols)
        {
            var symbolList = symbols.ToList();
            if (!symbolList.Any()) 
                return ServiceResult<List<StockSearchResult>>.Ok(new List<StockSearchResult>());

            try 
            {
                await EnsureCrumbAsync();
                
                var joinedSymbols = string.Join(",", symbolList.Select(Uri.EscapeDataString));
                var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={joinedSymbols}&crumb={_crumb}";
                
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("quoteResponse", out var quoteResponse) && 
                    quoteResponse.TryGetProperty("result", out var result) && 
                    result.ValueKind == JsonValueKind.Array)
                {
                    var searchResults = new List<StockSearchResult>();
                    
                    foreach (var item in result.EnumerateArray())
                    {
                        var quote = new StockSearchResult();
                        
                        if (item.TryGetProperty("symbol", out var sym))
                            quote.Symbol = sym.GetString() ?? "";
                            
                        if (item.TryGetProperty("regularMarketPrice", out var price))
                            quote.Price = price.GetDouble();
                            
                        if (item.TryGetProperty("regularMarketChange", out var change))
                            quote.Change = change.GetDouble();
                            
                        if (item.TryGetProperty("regularMarketChangePercent", out var changePct))
                            quote.ChangePercent = changePct.GetDouble();

                        if (item.TryGetProperty("shortName", out var name))
                            quote.Name = name.GetString() ?? "";
                        else if (item.TryGetProperty("longName", out var longName))
                            quote.Name = longName.GetString() ?? "";

                        if (!string.IsNullOrEmpty(quote.Symbol))
                        {
                            searchResults.Add(quote);
                        }
                    }
                    
                    return ServiceResult<List<StockSearchResult>>.Ok(searchResults);
                }
                
                return ServiceResult<List<StockSearchResult>>.Fail("Invalid response format from quote endpoint");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<StockSearchResult>>.Fail($"Failed to get quotes: {ex.Message}");
            }
        }

        public void SetStocks(List<Stock> stocks)
        {
            _stocks.Clear();
            _stocks.AddRange(stocks);
        }

        public Stock CreateStock(string symbol, string? name = null, string range = "1d")
        {
            symbol = symbol.ToUpper();
            if (string.IsNullOrEmpty(name)) name = symbol;
            
            var stock = new Stock
            {
                Symbol = symbol,
                Name = name
            };
            
            // Fire and forget update
            _ = UpdateAllStockDataAsync(stock, range);

            return stock;
        }

        private async Task UpdateAllStockDataAsync(Stock stock, string range)
        {
            await EnsureCrumbAsync();
            await Task.WhenAll(
                UpdateStockDataAsync(stock, range),
                UpdateStockAuxiliaryDataAsync(stock)
            );
        }

        private async Task UpdateStockAuxiliaryDataAsync(Stock stock)
        {
            await Task.WhenAll(
                UpdateStockEarningsAsync(stock),
                UpdateStockNewsAsync(stock),
                UpdateOptionsDataAsync(stock),
                UpdateInsiderDataAsync(stock),
                UpdateRVolDataAsync(stock)
            );
        }

        private async Task EnsureCrumbAsync()
        {
            if (!string.IsNullOrEmpty(_crumb) || _crumbAttempted) return;
            _crumbAttempted = true;

            try
            {
                // 1. Get Cookies
                await ExecuteWithRetryAsync(() => _httpClient.GetAsync("https://fc.yahoo.com"));
                
                // 2. Get Crumb
                string crumbUrl = "https://query1.finance.yahoo.com/v1/test/getcrumb";
                _crumb = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(crumbUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get crumb: {ex.Message}");
            }
        }

        public void AddStock(string symbol)
        {
            var stock = CreateStock(symbol);
            _stocks.Add(stock);
        }

        public void RemoveStock(Stock stock)
        {
            _stocks.Remove(stock);
        }

        public async Task<ServiceResult<bool>> UpdatePricesAsync(string range = "1d")
        {
            try
            {
                var tasks = _stocks.Concat(_indexes).Select(s => UpdateStockDataAsync(s, range));
                await Task.WhenAll(tasks);
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update prices: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> UpdateEarningsAsync()
        {
            try
            {
                var tasks = _stocks.Select(stock => UpdateStockEarningsAsync(stock));
                await Task.WhenAll(tasks);
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update earnings: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> UpdateNewsAsync()
        {
            try
            {
                var tasks = _stocks.Select(stock => UpdateStockNewsAsync(stock));
                await Task.WhenAll(tasks);
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update news: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> UpdateAuxiliaryDataAsync()
        {
            try
            {
                await EnsureCrumbAsync();
                var tasks = _stocks.Concat(_indexes).Select(stock => UpdateStockAuxiliaryDataAsync(stock));
                await Task.WhenAll(tasks);
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update auxiliary data: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> UpdateAllDataAsync(string range = "1d")
        {
            try
            {
                await EnsureCrumbAsync();
                var tasks = _stocks.Concat(_indexes).Select(stock => UpdateAllStockDataAsync(stock, range));
                await Task.WhenAll(tasks);
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update all data: {ex.Message}");
            }
        }

        public Task<ServiceResult<bool>> UpdateLogosAsync()
        {
            // Deprecated: Logos removed
            return Task.FromResult(ServiceResult<bool>.Ok(true));
        }

        private async Task UpdateStockNewsAsync(Stock stock)
        {
            try
            {
                // Request more items initially to allow for filtering
                var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={stock.Symbol}&newsCount=20";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("news", out var newsArray) && newsArray.ValueKind == JsonValueKind.Array)
                {
                    var newsItems = new List<NewsItem>();
                    
                    // Noise Filters
                    var blockedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "The Motley Fool", "Zacks Equity Research", "InvestorPlace", "Seeking Alpha", 
                        "Benzinga", "MarketWatch", "TheStreet", "TipRanks", "Simply Wall St", "GuruFocus",
                        "Barrons", "Bloomberg", "IBD", "Investors Business Daily"
                    };

                    var blockedPrefixes = new[] 
                    { 
                        "Why", "Prediction", "Here's", "3 Stocks", "5 Stocks", "7 Stocks", "Best", "Top", 
                        "Is ", "Should You", "Where Will", "Could ", "Forget ", "Opinion"
                    };

                    var blockedTerms = new[]
                    {
                        "Analyst", "Upgrade", "Downgrade", "Price Target", "Buy Rating", "Sell Rating", 
                        "Strong Buy", "Strong Sell", "Prediction"
                    };

                    // Determine cutoff date (Close of Business on the last full day of trading)
                    var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    var nowEastern = TimeZoneInfo.ConvertTime(DateTime.Now, easternZone);
                    DateTime cutoff;

                    // Helper to get previous weekday
                    DateTime GetPreviousWeekday(DateTime date)
                    {
                        do { date = date.AddDays(-1); }
                        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                        return date;
                    }

                    // Try to use stock data to determine the last full trading day
                    if (stock.Timestamps != null && stock.Timestamps.Count > 0)
                    {
                        // Timestamps are in Local Time (from UpdateStockDataAsync)
                        var lastTsLocal = stock.Timestamps.Last();
                        var lastTsEastern = TimeZoneInfo.ConvertTime(lastTsLocal, TimeZoneInfo.Local, easternZone);
                        
                        // Check if the last data point represents a market close (>= 15:55)
                        // Market closes at 16:00 ET.
                        bool isMarketClosedForDay = lastTsEastern.Hour >= 16 || (lastTsEastern.Hour == 15 && lastTsEastern.Minute >= 55);
                        
                        if (isMarketClosedForDay)
                        {
                            // We have a full day of data for this date
                            cutoff = lastTsEastern.Date.AddHours(16);
                        }
                        else
                        {
                            // Partial day (market still open, or half-day)
                            // Cutoff is the close of the PREVIOUS trading day
                            cutoff = GetPreviousWeekday(lastTsEastern.Date).AddHours(16);
                        }
                    }
                    else
                    {
                        // Fallback if no data available yet
                        if (nowEastern.DayOfWeek == DayOfWeek.Saturday || nowEastern.DayOfWeek == DayOfWeek.Sunday)
                        {
                             // Weekend -> Last Friday
                             var lastFri = nowEastern.Date;
                             while (lastFri.DayOfWeek != DayOfWeek.Friday) lastFri = lastFri.AddDays(-1);
                             cutoff = lastFri.AddHours(16);
                        }
                        else
                        {
                            // Weekday
                            if (nowEastern.Hour >= 16)
                            {
                                // After close -> Today
                                cutoff = nowEastern.Date.AddHours(16);
                            }
                            else
                            {
                                // Before close -> Yesterday (or Fri)
                                cutoff = GetPreviousWeekday(nowEastern.Date).AddHours(16);
                            }
                        }
                    }

                    foreach (var item in newsArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("providerPublishTime", out var timeProp))
                        {
                            long unixSeconds = timeProp.GetInt64();
                            var publishedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToOffset(easternZone.BaseUtcOffset).DateTime;

                            if (publishedAt > cutoff)
                            {
                                string title = item.GetProperty("title").GetString() ?? "";
                                string source = item.TryGetProperty("publisher", out var pub) ? pub.GetString() ?? "" : "";

                                // Apply Filters
                                if (blockedSources.Contains(source)) continue;
                                if (blockedPrefixes.Any(p => title.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
                                if (blockedTerms.Any(t => title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                                if (title.Contains("Earnings", StringComparison.OrdinalIgnoreCase)) continue; // Filter earnings noise as we have a flag for that

                                // Relevance Check: Must contain Symbol or Company Name
                                string simpleName = stock.Name.Split(' ')[0];
                                // Avoid matching very short common words if they happen to be the name (unlikely for major stocks but safe to check)
                                if (simpleName.Length < 3 && stock.Name.Contains(" ")) simpleName = stock.Name;

                                bool containsSymbol = title.IndexOf(stock.Symbol, StringComparison.OrdinalIgnoreCase) >= 0;
                                bool containsName = title.IndexOf(simpleName, StringComparison.OrdinalIgnoreCase) >= 0;

                                if (!containsSymbol && !containsName) continue;

                                string imageUrl = "";
                                if (item.TryGetProperty("thumbnail", out var thumb) && 
                                    thumb.TryGetProperty("resolutions", out var resolutions) && 
                                    resolutions.ValueKind == JsonValueKind.Array && 
                                    resolutions.GetArrayLength() > 0)
                                {
                                    // Get the second resolution if available (usually better quality), otherwise first
                                    var resIndex = resolutions.GetArrayLength() > 1 ? 1 : 0;
                                    if (resolutions[resIndex].TryGetProperty("url", out var urlProp))
                                    {
                                        imageUrl = urlProp.GetString() ?? "";
                                    }
                                }

                                var newsItem = new NewsItem
                                {
                                    Title = title,
                                    PublishedAt = publishedAt,
                                    Url = item.TryGetProperty("link", out var link) ? link.GetString() ?? "" : "",
                                    Source = source,
                                    ImageUrl = imageUrl
                                };

                                // Image pre-loading removed to improve startup speed
                                // UI will bind to ImageUrl directly

                                newsItems.Add(newsItem);
                            }
                        }
                    }

                    var finalItems = newsItems.OrderByDescending(n => n.PublishedAt).Take(2).ToList();
                    stock.NewsItems = finalItems;

                    // Background Image Pre-caching (Non-blocking)
                    _ = Task.Run(async () =>
                    {
                        foreach (var item in finalItems)
                        {
                            if (!string.IsNullOrEmpty(item.ImageUrl))
                            {
                                try
                                {
                                    var bytes = await _httpClient.GetByteArrayAsync(item.ImageUrl);
                                    
                                    if (System.Windows.Application.Current != null)
                                    {
                                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            try
                                            {
                                                var image = new BitmapImage();
                                                using (var mem = new MemoryStream(bytes))
                                                {
                                                    mem.Position = 0;
                                                    image.BeginInit();
                                                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                                    image.CacheOption = BitmapCacheOption.OnLoad;
                                                    image.UriSource = null;
                                                    image.StreamSource = mem;
                                                    image.EndInit();
                                                }
                                                image.Freeze();
                                                item.ImageSource = image;
                                            }
                                            catch { /* Ignore image creation errors */ }
                                        });
                                    }
                                }
                                catch { /* Ignore download errors */ }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update news for {stock.Symbol}: {ex.Message}");
            }
        }

        private async Task UpdateStockEarningsAsync(Stock stock)
        {
            try
            {
                double ParseDouble(JsonElement element, string propName)
                {
                    if (element.TryGetProperty(propName, out var prop))
                    {
                        if (prop.ValueKind == JsonValueKind.Number) return prop.GetDouble();
                        if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var val)) return val;
                    }
                    return 0;
                }

                // 1. Check for recent earnings (Beat/Miss)
                var surpriseUrl = $"https://api.nasdaq.com/api/company/{stock.Symbol}/earnings-surprise";
                var surpriseResponse = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(surpriseUrl));
                
                using var surpriseDoc = JsonDocument.Parse(surpriseResponse);
                
                if (surpriseDoc.RootElement.TryGetProperty("data", out var data) &&
                    data.ValueKind != JsonValueKind.Null &&
                    data.TryGetProperty("earningsSurpriseTable", out var table) &&
                    table.ValueKind != JsonValueKind.Null &&
                    table.TryGetProperty("rows", out var rows) &&
                    rows.ValueKind == JsonValueKind.Array &&
                    rows.GetArrayLength() > 0)
                {
                    var lastReport = rows[0];
                    if (lastReport.TryGetProperty("dateReported", out var dateProp) && dateProp.GetString() is string dateReportedStr)
                    {
                        if (DateTime.TryParse(dateReportedStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateReported))
                        {
                            // Check if reported within last 7 days
                            if ((DateTime.Now.Date - dateReported.Date).TotalDays <= 7 && (DateTime.Now.Date - dateReported.Date).TotalDays >= 0)
                            {
                                var eps = ParseDouble(lastReport, "eps");
                                var forecast = ParseDouble(lastReport, "consensusForecast");
                                var percentSurprise = ParseDouble(lastReport, "percentageSurprise");
                                
                                // Normalize percentage (API returns 23.5 for 23.5%, Model expects 0.235)
                                stock.EarningsSurprisePercent = percentSurprise / 100.0;

                                if (percentSurprise > 0)
                                {
                                    stock.EarningsStatus = "Beat";
                                    stock.EarningsMessage = $"Earnings Beat!\nReported: {dateReported:d}\nActual: {eps}\nForecast: {forecast}\nBeat by: {percentSurprise}%";
                                }
                                else
                                {
                                    stock.EarningsStatus = "Miss";
                                    stock.EarningsMessage = $"Earnings Miss.\nReported: {dateReported:d}\nActual: {eps}\nForecast: {forecast}\nMissed by: {Math.Abs(percentSurprise)}%";
                                }
                                return; // Found recent earnings, no need to check upcoming
                            }
                        }
                    }
                }

                // If we haven't returned by now, it means no recent earnings beat/miss was found.
                // Reset status to None before checking upcoming, so we don't keep stale flags.
                stock.EarningsStatus = "None";

                // 2. Check for upcoming earnings
                // Use analyst endpoint as company endpoint often returns 404
                var dateUrl = $"https://api.nasdaq.com/api/analyst/{stock.Symbol}/earnings-date";
                var dateResponse = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(dateUrl));
                
                using var dateDoc = JsonDocument.Parse(dateResponse);
                
                if (dateDoc.RootElement.TryGetProperty("data", out var dateData) &&
                    dateData.ValueKind != JsonValueKind.Null)
                {
                    // Try to parse from announcement string: "Earnings announcement* for LULU: Dec 11, 2025"
                    if (dateData.TryGetProperty("announcement", out var announcementProp) && 
                        announcementProp.GetString() is string announcement)
                    {
                        var parts = announcement.Split(':');
                        if (parts.Length > 1)
                        {
                            var dateStr = parts[1].Trim();
                            // Try parsing "Dec 11, 2025"
                            if (DateTime.TryParse(dateStr, new CultureInfo("en-US"), DateTimeStyles.None, out var earningsDate))
                            {
                                // Check if upcoming in next 7 days (approx 5 business days)
                                if (earningsDate >= DateTime.Now.Date && (earningsDate - DateTime.Now.Date).TotalDays <= 7)
                                {
                                    stock.EarningsStatus = "Upcoming";
                                    stock.EarningsDate = earningsDate;
                                    stock.EarningsMessage = $"Earnings Upcoming\nDate: {earningsDate:d}";
                                }
                                else
                                {
                                    stock.EarningsStatus = "None";
                                }
                            }
                        }
                    }
                }
                else
                {
                    // No upcoming earnings data found, and no recent earnings found (or we wouldn't be here)
                    stock.EarningsStatus = "None";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update earnings for {stock.Symbol}: {ex.Message}");
                // Expose error in UI for debugging
                stock.EarningsMessage = $"Error: {ex.Message}";
                if (stock.EarningsStatus == null) stock.EarningsStatus = "None";
            }
        }

        private async Task UpdateOptionsDataAsync(Stock stock)
        {
            try
            {
                var url = $"https://query2.finance.yahoo.com/v7/finance/options/{stock.Symbol}?crumb={_crumb}";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                var result = doc.RootElement.GetProperty("optionChain").GetProperty("result")[0];
                var options = result.GetProperty("options")[0];
                
                // Get Expiration Date
                if (options.TryGetProperty("expirationDate", out var expDateProp))
                {
                    long unixSeconds = expDateProp.GetInt64();
                    stock.OptionsImpactDate = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
                }

                var calls = options.GetProperty("calls");
                var puts = options.GetProperty("puts");

                long callVol = 0;
                long putVol = 0;
                long callOI = 0;
                long putOI = 0;

                // Simplified Max Pain Calculation
                // We need a list of all strikes and their OI
                var strikes = new Dictionary<double, (long callOI, long putOI)>();

                foreach (var call in calls.EnumerateArray())
                {
                    callVol += call.TryGetProperty("volume", out var v) ? (long)v.GetDouble() : 0; // Volume is sometimes double in JSON
                    long oi = call.TryGetProperty("openInterest", out var o) ? (long)o.GetDouble() : 0;
                    callOI += oi;
                    
                    double strike = call.GetProperty("strike").GetDouble();
                    if (!strikes.ContainsKey(strike)) strikes[strike] = (0, 0);
                    strikes[strike] = (strikes[strike].callOI + oi, strikes[strike].putOI);
                }

                foreach (var put in puts.EnumerateArray())
                {
                    putVol += put.TryGetProperty("volume", out var v) ? (long)v.GetDouble() : 0;
                    long oi = put.TryGetProperty("openInterest", out var o) ? (long)o.GetDouble() : 0;
                    putOI += oi;

                    double strike = put.GetProperty("strike").GetDouble();
                    if (!strikes.ContainsKey(strike)) strikes[strike] = (0, 0);
                    strikes[strike] = (strikes[strike].callOI, strikes[strike].putOI + oi);
                }

                stock.CallVolume = callVol;
                stock.PutVolume = putVol;
                stock.TotalVolume = callVol + putVol;
                stock.OpenInterest = callOI + putOI;

                // Calculate Max Pain
                double minPainValue = double.MaxValue;
                double maxPainStrike = 0;

                foreach (var strikeCandidate in strikes.Keys)
                {
                    double totalPain = 0;
                    foreach (var kvp in strikes)
                    {
                        double strike = kvp.Key;
                        long cOI = kvp.Value.callOI;
                        long pOI = kvp.Value.putOI;

                        // If price expires at strikeCandidate:
                        // Calls are ITM if strike < strikeCandidate
                        if (strike < strikeCandidate)
                        {
                            totalPain += (strikeCandidate - strike) * cOI;
                        }
                        // Puts are ITM if strike > strikeCandidate
                        if (strike > strikeCandidate)
                        {
                            totalPain += (strike - strikeCandidate) * pOI;
                        }
                    }

                    if (totalPain < minPainValue)
                    {
                        minPainValue = totalPain;
                        maxPainStrike = strikeCandidate;
                    }
                }

                stock.MaxPainPrice = (decimal)maxPainStrike;
                
                // Check for Unusual Volume (Simplified: Volume > OI for any strike, or Total Vol > Avg Vol proxy)
                // For now, let's just say if Total Volume is high relative to something, but we don't have historical options volume.
                // Let's use a simple heuristic: If Call/Put Ratio is extreme (> 2 or < 0.5) AND Volume is significant (> 1000)
                if (stock.TotalVolume > 1000 && (stock.CallVolume > 2 * stock.PutVolume || stock.PutVolume > 2 * stock.CallVolume))
                {
                    stock.UnusualOptionsVolume = true;
                }
                else
                {
                    stock.UnusualOptionsVolume = false;
                }

                stock.RefreshDirectionalConfidence();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update options for {stock.Symbol}: {ex.Message}");
            }
        }

        private async Task UpdateInsiderDataAsync(Stock stock)
        {
            try
            {
                var url = $"https://query2.finance.yahoo.com/v10/finance/quoteSummary/{stock.Symbol}?modules=insiderTransactions,netSharePurchaseActivity,institutionOwnership&crumb={_crumb}";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result")[0];

                // Insider Transactions List
                if (result.TryGetProperty("insiderTransactions", out var transactions) && transactions.TryGetProperty("transactions", out var transArray))
                {
                    var list = new List<InsiderTransaction>();
                    foreach (var t in transArray.EnumerateArray())
                    {
                        if (t.TryGetProperty("startDate", out var dateProp) && dateProp.TryGetProperty("fmt", out var dateFmt))
                        {
                            var dateStr = dateFmt.GetString();
                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                var person = t.GetProperty("filerName").GetString() ?? "Unknown";
                                var shares = t.GetProperty("shares").GetProperty("raw").GetDouble();
                                var value = t.TryGetProperty("value", out var v) && v.TryGetProperty("raw", out var vr) ? (decimal)vr.GetDouble() : 0;
                                
                                // If value is missing, estimate
                                if (value == 0 && shares > 0) value = (decimal)shares * stock.Price;

                                // Determine type (Buy/Sell) based on text or shares sign?
                                // Usually "Sale" or "Purchase" in transactionText
                                string type = "Buy";
                                string text = t.GetProperty("transactionText").GetString() ?? "";
                                if (text.Contains("Sale", StringComparison.OrdinalIgnoreCase) || text.Contains("Sold", StringComparison.OrdinalIgnoreCase))
                                    type = "Sell";
                                
                                list.Add(new InsiderTransaction
                                {
                                    Date = date,
                                    Person = person,
                                    TransactionType = type,
                                    Value = value
                                });
                            }
                        }
                    }
                    
                    // Filter to last 6 months to be relevant
                    var cutoff = DateTime.Now.AddMonths(-6);
                    var recentTransactions = list.Where(x => x.Date >= cutoff).OrderByDescending(x => x.Date).Take(10).ToList();
                    
                    stock.InsiderTransactions = recentTransactions;

                    // Calculate Net Insider Value from these specific transactions
                    // This ensures our "Genius" signal logic matches the data shown to the user
                    // and avoids the massive aggregate numbers from 'netSharePurchaseActivity'
                    decimal netValue = 0;
                    foreach (var tx in recentTransactions)
                    {
                        if (tx.TransactionType == "Buy")
                            netValue += tx.Value;
                        else
                            netValue -= tx.Value;
                    }
                    stock.NetInsiderTransactionValue = netValue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update insider data for {stock.Symbol}: {ex.Message}");
            }
        }

        private async Task UpdateRVolDataAsync(Stock stock)
        {
            try
            {
                var url = $"https://query2.finance.yahoo.com/v10/finance/quoteSummary/{stock.Symbol}?modules=summaryDetail,price&crumb={_crumb}";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result")[0];

                long avgVolume = 0;
                long currentVolume = 0;

                if (result.TryGetProperty("summaryDetail", out var summary) && summary.TryGetProperty("averageVolume", out var avgVolProp))
                {
                    avgVolume = (long)avgVolProp.GetProperty("raw").GetDouble();
                }
                else if (result.TryGetProperty("price", out var price) && price.TryGetProperty("averageDailyVolume3Month", out var avgVol3m))
                {
                    avgVolume = (long)avgVol3m.GetProperty("raw").GetDouble();
                }

                if (result.TryGetProperty("price", out var priceModule) && priceModule.TryGetProperty("regularMarketVolume", out var volProp))
                {
                    currentVolume = (long)volProp.GetProperty("raw").GetDouble();
                }

                // Market Cap
                if (result.TryGetProperty("summaryDetail", out var summaryDetail) && summaryDetail.TryGetProperty("marketCap", out var capProp))
                {
                    stock.MarketCap = (decimal)capProp.GetProperty("raw").GetDouble();
                }
                else if (result.TryGetProperty("price", out var priceMod) && priceMod.TryGetProperty("marketCap", out var capProp2))
                {
                    stock.MarketCap = (decimal)capProp2.GetProperty("raw").GetDouble();
                }

                stock.AverageVolume = avgVolume;
                stock.CurrentVolume = currentVolume;
                
                // For RVOL, we ideally want "Average Volume at this time of day".
                // Since we don't have that granular data easily without storing history, 
                // we will use a simplified model:
                // Expected Volume = Average Volume * DayProgress
                // RVOL = Current Volume / Expected Volume
                
                // However, volume is U-shaped (high at open/close). Linear approximation is poor but better than nothing.
                // Let's just use the full day average for now as the denominator for "AverageVolumeByTimeOfDay" 
                // but scale it by progress if we want "Pacing".
                // The prompt asked for "Relative Volume (RVOL) > 1.5". Usually this means "Current Volume / Average Volume for this time of day".
                // If we use full day average, RVOL will be low in the morning.
                
                // Let's use the linear approximation for now:
                if (stock.DayProgress > 0.05) // Avoid division by near-zero at open
                {
                    stock.AverageVolumeByTimeOfDay = (long)(avgVolume * stock.DayProgress);
                }
                else
                {
                    stock.AverageVolumeByTimeOfDay = (long)(avgVolume * 0.05); // Floor at 5%
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update RVOL for {stock.Symbol}: {ex.Message}");
            }
        }

        private class ChartResult
        {
            public double Price { get; set; }
            public double PreviousClose { get; set; }
            public long RegularMarketTime { get; set; }
            public List<double> History { get; set; } = new();
            public List<DateTime> Timestamps { get; set; } = new();
        }

        private async Task<ChartResult?> FetchChartDataAsync(string symbol, string range)
        {
            try
            {
                string interval = "5m";
                switch (range)
                {
                    case "1d": interval = "5m"; break;
                    case "5d": interval = "15m"; break;
                    case "1mo": interval = "60m"; break;
                    case "1y": interval = "1d"; break;
                    case "5y": interval = "1wk"; break;
                    case "10y": interval = "1mo"; break;
                    default: interval = "5m"; break;
                }

                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval={interval}&range={range}";
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetStringAsync(url));
                
                using var doc = JsonDocument.Parse(response);
                var chart = doc.RootElement.GetProperty("chart");
                
                if (chart.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    return null;
                }

                var result = chart.GetProperty("result")[0];
                var meta = result.GetProperty("meta");
                
                var data = new ChartResult();

                if (meta.TryGetProperty("regularMarketPrice", out var priceProp))
                    data.Price = priceProp.GetDouble();
                
                if (meta.TryGetProperty("regularMarketTime", out var timeProp))
                    data.RegularMarketTime = timeProp.GetInt64();

                if (meta.TryGetProperty("chartPreviousClose", out var prevCloseProp))
                    data.PreviousClose = prevCloseProp.GetDouble();
                else if (meta.TryGetProperty("previousClose", out var prevCloseProp2))
                    data.PreviousClose = prevCloseProp2.GetDouble();

                if (result.TryGetProperty("timestamp", out var timestamps) && 
                    result.TryGetProperty("indicators", out var indicators) &&
                    indicators.TryGetProperty("quote", out var quoteArray) &&
                    quoteArray.GetArrayLength() > 0 &&
                    quoteArray[0].TryGetProperty("close", out var closes))
                {
                    var timestampList = timestamps.EnumerateArray().ToList();
                    var closeList = closes.EnumerateArray().ToList();

                    for (int i = 0; i < Math.Min(timestampList.Count, closeList.Count); i++)
                    {
                        if (closeList[i].ValueKind != JsonValueKind.Null)
                        {
                            data.History.Add(closeList[i].GetDouble());
                            long unixSeconds = timestampList[i].GetInt64();
                            data.Timestamps.Add(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime);
                        }
                    }
                }
                
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch chart data for {symbol}: {ex.Message}");
                return null;
            }
        }

        private async Task UpdateStockDataAsync(Stock stock, string range)
        {
            try
            {
                // 1. Fetch Main Range Data
                var mainData = await FetchChartDataAsync(stock.Symbol, range);
                if (mainData == null) return;

                // Update Stock Price immediately
                stock.Price = (decimal)mainData.Price;
                
                // Always update history (clears it if empty)
                stock.History = mainData.History;
                stock.Timestamps = mainData.Timestamps;

                if (range == "1d")
                {
                    // Intraday: Use previous close from meta
                    if (mainData.PreviousClose > 0)
                    {
                        stock.Change = (decimal)(mainData.Price - mainData.PreviousClose);
                        stock.ChangePercent = (mainData.Price - mainData.PreviousClose) / mainData.PreviousClose * 100;
                    }

                    // Calculate Day Progress
                    // Market hours: 9:30 AM - 4:00 PM ET
                    try
                    {
                        var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        var now = TimeZoneInfo.ConvertTime(DateTime.Now, easternZone);
                        
                        // Check if data is from a previous day
                        bool isPreviousDayData = false;
                        if (mainData.RegularMarketTime > 0)
                        {
                            var marketTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(mainData.RegularMarketTime), easternZone).DateTime;
                            if (marketTime.Date < now.Date)
                            {
                                isPreviousDayData = true;
                            }
                        }

                        // Also check for weekends explicitly just in case
                        bool isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;

                        if (isPreviousDayData || isWeekend)
                        {
                            stock.DayProgress = 1.0;
                        }
                        else
                        {
                            var today = now.Date;
                            var marketOpen = today.AddHours(9).AddMinutes(30);
                            var marketClose = today.AddHours(16);

                            if (now < marketOpen)
                            {
                                stock.DayProgress = 0;
                            }
                            else if (now > marketClose)
                            {
                                stock.DayProgress = 1.0;
                            }
                            else
                            {
                                var totalMinutes = (marketClose - marketOpen).TotalMinutes;
                                var elapsedMinutes = (now - marketOpen).TotalMinutes;
                                stock.DayProgress = Math.Max(0, Math.Min(1.0, elapsedMinutes / totalMinutes));
                            }
                        }
                    }
                    catch
                    {
                        stock.DayProgress = 1.0;
                    }

                    // Populate Intraday Properties (Same as Main)
                    stock.IntradayChange = stock.Change;
                    stock.IntradayChangePercent = stock.ChangePercent;
                    stock.IntradayHistory = new List<double>(stock.History);
                    stock.IntradayTimestamps = new List<DateTime>(stock.Timestamps);
                }
                else
                {
                    // Historical: Use first data point as baseline
                    stock.DayProgress = 1.0;
                    if (stock.History.Count > 0)
                    {
                        double baseline = stock.History[0];
                        stock.Change = (decimal)(mainData.Price - baseline);
                        stock.ChangePercent = (mainData.Price - baseline) / baseline * 100;
                    }
                    else if (mainData.PreviousClose > 0)
                    {
                        // Fallback if no history but we have previous close
                        stock.Change = (decimal)(mainData.Price - mainData.PreviousClose);
                        stock.ChangePercent = (mainData.Price - mainData.PreviousClose) / mainData.PreviousClose * 100;
                    }

                    // Fetch Intraday Data Separately for Floating Window
                    var intradayData = await FetchChartDataAsync(stock.Symbol, "1d");
                    if (intradayData != null)
                    {
                        if (intradayData.PreviousClose > 0)
                        {
                            stock.IntradayChange = (decimal)(intradayData.Price - intradayData.PreviousClose);
                            stock.IntradayChangePercent = (intradayData.Price - intradayData.PreviousClose) / intradayData.PreviousClose * 100;
                        }
                        stock.IntradayHistory = intradayData.History;
                        stock.IntradayTimestamps = intradayData.Timestamps;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update {stock.Symbol}: {ex.Message}");
            }
        }
    }
}
