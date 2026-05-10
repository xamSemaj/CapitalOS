using CapitalOS.Models;

namespace CapitalOS.Services
{
        public interface IStockMarketService
        {
            Task<MarketHomeViewModel> GetHomeMarketDataAsync(string symbol);
            Task<StockQuoteViewModel> GetLatestQuoteAsync(string symbol);

            Task<StockSearchViewModel> GetStockDiscoveryAsync(string? query);
        }

}
