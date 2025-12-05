using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public class StockService : IStockService
    {
        private readonly List<Stock> _stocks;
        private readonly List<Stock> _indexes;
        private readonly HttpClient _httpClient;

        public StockService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
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
                new Stock { Symbol = "AMD", Name = "Adv Micro Dev" },
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
                var response = await _httpClient.GetStringAsync(url);
                
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
                // Use chart endpoint concurrently as quote endpoint is often blocked
                var tasks = symbolList.Select(async symbol =>
                {
                    try
                    {
                        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d";
                        var response = await _httpClient.GetStringAsync(url);
                        
                        using var doc = JsonDocument.Parse(response);
                        var chart = doc.RootElement.GetProperty("chart");
                        
                        if (chart.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                            return null;

                        var result = chart.GetProperty("result")[0];
                        var meta = result.GetProperty("meta");

                        var quote = new StockSearchResult { Symbol = symbol };

                        if (meta.TryGetProperty("regularMarketPrice", out var priceProp))
                            quote.Price = priceProp.GetDouble();

                        double previousClose = 0;
                        if (meta.TryGetProperty("chartPreviousClose", out var prevCloseProp))
                            previousClose = prevCloseProp.GetDouble();
                        else if (meta.TryGetProperty("previousClose", out var prevCloseProp2))
                            previousClose = prevCloseProp2.GetDouble();

                        if (previousClose > 0 && quote.Price.HasValue)
                        {
                            quote.Change = quote.Price.Value - previousClose;
                            quote.ChangePercent = (quote.Price.Value - previousClose) / previousClose * 100;
                        }

                        return quote;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get quote for {symbol}: {ex.Message}");
                        return null;
                    }
                });

                var results = await Task.WhenAll(tasks);
                return ServiceResult<List<StockSearchResult>>.Ok(results.Where(r => r != null).Cast<StockSearchResult>().ToList());
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
            _ = UpdateStockDataAsync(stock, range);
            _ = UpdateStockEarningsAsync(stock);

            return stock;
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
                // Process sequentially or in small batches to avoid rate limits
                foreach (var stock in _stocks)
                {
                    await UpdateStockEarningsAsync(stock);
                    // Small delay to be nice to the API
                    await Task.Delay(500);
                }
                return ServiceResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Fail($"Failed to update earnings: {ex.Message}");
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
                var surpriseResponse = await _httpClient.GetStringAsync(surpriseUrl);
                
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
                            // Check if reported within last 3 days
                            if ((DateTime.Now.Date - dateReported.Date).TotalDays <= 3 && (DateTime.Now.Date - dateReported.Date).TotalDays >= 0)
                            {
                                var eps = ParseDouble(lastReport, "eps");
                                var forecast = ParseDouble(lastReport, "consensusForecast");
                                var percentSurprise = ParseDouble(lastReport, "percentageSurprise");
                                
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
                var dateResponse = await _httpClient.GetStringAsync(dateUrl);
                
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

        private async Task UpdateStockDataAsync(Stock stock, string range)
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

                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(stock.Symbol)}?interval={interval}&range={range}";
                var response = await _httpClient.GetStringAsync(url);
                
                using var doc = JsonDocument.Parse(response);
                var chart = doc.RootElement.GetProperty("chart");
                
                if (chart.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    return;
                }

                var result = chart.GetProperty("result")[0];
                var meta = result.GetProperty("meta");
                
                double regularMarketPrice = 0;
                double previousClose = 0;

                if (meta.TryGetProperty("regularMarketPrice", out var priceProp))
                    regularMarketPrice = priceProp.GetDouble();
                
                if (meta.TryGetProperty("chartPreviousClose", out var prevCloseProp))
                    previousClose = prevCloseProp.GetDouble();
                else if (meta.TryGetProperty("previousClose", out var prevCloseProp2))
                    previousClose = prevCloseProp2.GetDouble();

                // Update Stock Price immediately (even if no chart data)
                stock.Price = (decimal)regularMarketPrice;

                // History for Sparkline
                var history = new List<double>();
                var timeList = new List<DateTime>();

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
                            history.Add(closeList[i].GetDouble());
                            long unixSeconds = timestampList[i].GetInt64();
                            timeList.Add(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime);
                        }
                    }
                }
                
                // Always update history (clears it if empty)
                stock.History = history;
                stock.Timestamps = timeList;

                if (range == "1d")
                {
                    // Intraday: Use previous close from meta
                    if (previousClose > 0)
                    {
                        stock.Change = (decimal)(regularMarketPrice - previousClose);
                        stock.ChangePercent = (regularMarketPrice - previousClose) / previousClose * 100;
                    }

                    // Calculate Day Progress
                    // Market hours: 9:30 AM - 4:00 PM ET
                    try
                    {
                        var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        var now = TimeZoneInfo.ConvertTime(DateTime.Now, easternZone);
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
                    catch
                    {
                        stock.DayProgress = 1.0;
                    }
                }
                else
                {
                    // Historical: Use first data point as baseline
                    stock.DayProgress = 1.0;
                    if (history.Count > 0)
                    {
                        double baseline = history[0];
                        stock.Change = (decimal)(regularMarketPrice - baseline);
                        stock.ChangePercent = (regularMarketPrice - baseline) / baseline * 100;
                    }
                    else if (previousClose > 0)
                    {
                        // Fallback if no history but we have previous close (e.g. mutual fund with no data for range)
                        stock.Change = (decimal)(regularMarketPrice - previousClose);
                        stock.ChangePercent = (regularMarketPrice - previousClose) / previousClose * 100;
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
