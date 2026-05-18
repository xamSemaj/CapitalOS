using CapitalOS.Models;
using CapitalOS.Services;
using Microsoft.AspNetCore.Mvc;

namespace CapitalOS.Controllers
{
    public class StocksController : Controller
    {
        private readonly IStockMarketService _stockMarketService;

        public StocksController(IStockMarketService stockMarketService)
        {
            _stockMarketService = stockMarketService;
        }

        public async Task<IActionResult> Index(string? query)
        {
            try
            {
                var model = await _stockMarketService.GetStockDiscoveryAsync(query);
                return View(model);
            }
            catch (Exception ex)
            {
                var fallbackModel = new MarketHomeViewModel
                {
                    FeaturedSymbol =  "NVDA",
                    FeaturedCompanyName = "Market data unavailable",
                    CurrentPrice = 0,
                    ChangePercent = 0,
                    ChartPoints = new List<StockChartPoint>()
                };
                ViewBag.ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"HomeController Error: {ex}");
                return View(fallbackModel);
            }
        }
    }
}
