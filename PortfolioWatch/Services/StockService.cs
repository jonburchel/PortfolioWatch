using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public class StockService
    {
        private readonly List<Stock> _stocks;
        private readonly List<Stock> _indexes;
        private readonly HttpClient _httpClient;

        public StockService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

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

        public Task<List<Stock>> GetStocksAsync()
        {
            return Task.FromResult(_stocks);
        }

        public Task<List<Stock>> GetIndexesAsync()
        {
            return Task.FromResult(_indexes);
        }

        public async Task<List<(string Symbol, string Name)>> SearchStocksAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<(string, string)>();

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
                
                return results;
            }
            catch
            {
                return new List<(string, string)>();
            }
        }

        public async Task<(string Symbol, string Name)> GetStockDetailsAsync(string query)
        {
            var results = await SearchStocksAsync(query);
            var match = results.FirstOrDefault(r => r.Symbol.Equals(query, StringComparison.OrdinalIgnoreCase));
            
            if (match.Symbol == null && results.Count > 0)
            {
                // If no exact match, take the first one
                match = results[0];
            }
            
            return match;
        }

        public void SetStocks(List<Stock> stocks)
        {
            _stocks.Clear();
            _stocks.AddRange(stocks);
        }

        public Stock CreateStock(string symbol, string? name = null)
        {
            symbol = symbol.ToUpper();
            if (string.IsNullOrEmpty(name)) name = symbol;
            
            var stock = new Stock
            {
                Symbol = symbol,
                Name = name
            };
            
            // Fire and forget update
            _ = UpdateStockDataAsync(stock);

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

        public async Task UpdatePricesAsync()
        {
            var tasks = _stocks.Concat(_indexes).Select(UpdateStockDataAsync);
            await Task.WhenAll(tasks);
        }

        private async Task UpdateStockDataAsync(Stock stock)
        {
            try
            {
                // interval=5m for decent granularity for sparkline, range=1d for today's action
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(stock.Symbol)}?interval=5m&range=1d";
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

                // Update Stock
                stock.Price = (decimal)regularMarketPrice;
                
                if (previousClose > 0)
                {
                    stock.Change = (decimal)(regularMarketPrice - previousClose);
                    stock.ChangePercent = (regularMarketPrice - previousClose) / previousClose * 100;
                }

                // History for Sparkline
                if (result.TryGetProperty("timestamp", out var timestamps) && 
                    result.TryGetProperty("indicators", out var indicators) &&
                    indicators.TryGetProperty("quote", out var quoteArray) &&
                    quoteArray.GetArrayLength() > 0 &&
                    quoteArray[0].TryGetProperty("close", out var closes))
                {
                    var history = new List<double>();
                    foreach (var close in closes.EnumerateArray())
                    {
                        if (close.ValueKind != JsonValueKind.Null)
                        {
                            history.Add(close.GetDouble());
                        }
                    }
                    stock.History = history;

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
                        // Fallback to 1.0 if timezone not found or other error
                        stock.DayProgress = 1.0;
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
