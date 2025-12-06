using System.Collections.Generic;
using System.Threading.Tasks;
using PortfolioWatch.Models;

namespace PortfolioWatch.Services
{
    public interface IStockService
    {
        Task<ServiceResult<List<Stock>>> GetStocksAsync();
        Task<ServiceResult<List<Stock>>> GetIndexesAsync();
        Task<ServiceResult<List<(string Symbol, string Name)>>> SearchStocksAsync(string query);
        Task<ServiceResult<(string Symbol, string Name)>> GetStockDetailsAsync(string query);
        Task<ServiceResult<List<StockSearchResult>>> GetQuotesAsync(IEnumerable<string> symbols);
        void SetStocks(List<Stock> stocks);
        Stock CreateStock(string symbol, string? name = null, string range = "1d");
        void AddStock(string symbol);
        void RemoveStock(Stock stock);
        Task<ServiceResult<bool>> UpdatePricesAsync(string range = "1d");
        Task<ServiceResult<bool>> UpdateEarningsAsync();
        Task<ServiceResult<bool>> UpdateNewsAsync();
        Task<ServiceResult<bool>> UpdateAllDataAsync(string range = "1d");
    }
}
