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
        private readonly Random _random = new Random();
        private readonly List<Stock> _stocks;
        private readonly List<Stock> _indexes;
        private readonly Dictionary<string, List<double>> _fullDayCurves = new Dictionary<string, List<double>>();
        private readonly HttpClient _httpClient;

        // Market Hours (ET)
        private readonly TimeSpan _marketOpen = new TimeSpan(9, 30, 0);
        private readonly TimeSpan _marketClose = new TimeSpan(16, 0, 0);

        public StockService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            _indexes = new List<Stock>
            {
                new Stock { Symbol = "^DJI", Name = "Dow Jones", Price = 36245.50m },
                new Stock { Symbol = "^IXIC", Name = "Nasdaq", Price = 14305.00m },
                new Stock { Symbol = "^GSPC", Name = "S&P 500", Price = 4585.59m }
            };

            _stocks = GetDefaultStocks();

            // Initialize full day curves
            foreach (var stock in _stocks.Concat(_indexes))
            {
                GenerateFullDayCurve(stock);
            }
            
            UpdatePrices();
        }

        public List<Stock> GetDefaultStocks()
        {
            return new List<Stock>
            {
                new Stock { Symbol = "MSFT", Name = "Microsoft Corp", Price = 375.00m },
                new Stock { Symbol = "AAPL", Name = "Apple Inc", Price = 190.00m },
                new Stock { Symbol = "GOOGL", Name = "Alphabet Inc", Price = 135.00m },
                new Stock { Symbol = "AMZN", Name = "Amazon.com Inc", Price = 145.00m },
                new Stock { Symbol = "TSLA", Name = "Tesla Inc", Price = 240.00m },
                new Stock { Symbol = "NVDA", Name = "NVIDIA Corp", Price = 480.00m },
                new Stock { Symbol = "META", Name = "Meta Platforms", Price = 330.00m },
                new Stock { Symbol = "NFLX", Name = "Netflix Inc", Price = 475.00m },
                new Stock { Symbol = "AMD", Name = "Adv Micro Dev", Price = 120.00m },
                new Stock { Symbol = "INTC", Name = "Intel Corp", Price = 44.00m }
            };
        }

        private void GenerateFullDayCurve(Stock stock)
        {
            var curve = new List<double>();
            double current = (double)stock.Price; // Start price (Open)
            
            // Generate 390 points (minutes in trading day)
            for (int i = 0; i < 390; i++)
            {
                curve.Add(current);
                // Random walk
                current = current * (1 + (_random.NextDouble() * 0.004 - 0.002));
            }
            
            _fullDayCurves[stock.Symbol] = curve;
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
            
            // Ensure curves exist for restored stocks
            foreach (var stock in _stocks)
            {
                if (!_fullDayCurves.ContainsKey(stock.Symbol))
                {
                    GenerateFullDayCurve(stock);
                }
            }
            
            UpdatePrices();
        }

        public Stock CreateStock(string symbol, string? name = null)
        {
            symbol = symbol.ToUpper();
            if (string.IsNullOrEmpty(name)) name = symbol;
            
            var price = (decimal)(_random.NextDouble() * 500 + 10);
            var stock = new Stock
            {
                Symbol = symbol,
                Name = name,
                Price = price
            };
            
            GenerateFullDayCurve(stock);
            UpdateStockData(stock); // Initial update

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
            if (_fullDayCurves.ContainsKey(stock.Symbol))
            {
                _fullDayCurves.Remove(stock.Symbol);
            }
        }

        public void UpdatePrices()
        {
            foreach (var stock in _stocks.Concat(_indexes))
            {
                UpdateStockData(stock);
            }
        }

        private void UpdateStockData(Stock stock)
        {
            if (!_fullDayCurves.ContainsKey(stock.Symbol))
            {
                GenerateFullDayCurve(stock);
            }

            var curve = _fullDayCurves[stock.Symbol];
            var now = DateTime.Now;
            // For testing/demo, let's assume we are in ET or just use local time
            // If outside market hours, show full day or empty?
            // User said: "If after close, we'll show a full day's action from the prior trading day."
            // "If we are mid-day, let's scale the graph"
            
            // Let's simulate "Now" being within market hours for demo purposes if it's weekend/night,
            // OR just use actual time.
            // Given the prompt implies a live feel, let's map current time to market time.
            
            double progress = 0;
            int pointsToShow = 0;

            var todayOpen = DateTime.Today.Add(_marketOpen);
            var todayClose = DateTime.Today.Add(_marketClose);

            if (now < todayOpen)
            {
                // Before open: Show yesterday's full close? Or empty?
                // "prior trading day" -> Full curve.
                progress = 1.0;
                pointsToShow = 390;
            }
            else if (now > todayClose)
            {
                // After close: Full day
                progress = 1.0;
                pointsToShow = 390;
            }
            else
            {
                // Mid-day
                var totalMinutes = (todayClose - todayOpen).TotalMinutes; // 390
                var elapsed = (now - todayOpen).TotalMinutes;
                progress = elapsed / totalMinutes;
                pointsToShow = (int)elapsed;
                if (pointsToShow < 1) pointsToShow = 1;
                if (pointsToShow > 390) pointsToShow = 390;
            }

            // Update Stock Properties
            var currentHistory = curve.Take(pointsToShow).ToList();
            if (currentHistory.Count > 0)
            {
                double openPrice = curve[0];
                double currentPrice = currentHistory.Last();
                
                stock.Price = (decimal)currentPrice;
                stock.Change = (decimal)(currentPrice - openPrice);
                stock.ChangePercent = (currentPrice - openPrice) / openPrice * 100;
                stock.History = currentHistory;
                stock.DayProgress = progress;
            }
        }
    }
}
