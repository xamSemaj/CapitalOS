using CapitalOS.Models;
using CapitalOS.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CapitalOS.Controllers
{
  
    public class HomeController : Controller
    {
        private readonly IStockMarketService _stockMarketService;

        public HomeController(IStockMarketService stockMarketService)
        {
            _stockMarketService = stockMarketService;
        }

    


        public async Task<IActionResult> Index(string symbol = "NVDA")
        {
            try
            {
                symbol = string.IsNullOrWhiteSpace(symbol)
                    ? "NVDA"
                    : symbol.Trim().ToUpper();

                var model = await _stockMarketService.GetHomeMarketDataAsync(symbol);

                return View(model);
            }
            catch
            {
                var fallbackModel = new MarketHomeViewModel
                {
                    FeaturedSymbol = symbol?.ToUpper() ?? "NVDA",
                    FeaturedCompanyName = "Market data unavailable",
                    CurrentPrice = 0,
                    ChangePercent = 0,
                    ChartPoints = new List<StockChartPoint>()
                };

                ViewBag.ErrorMessage = "Live market data is currently unavailable.";

                return View(fallbackModel);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
