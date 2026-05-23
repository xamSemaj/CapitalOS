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
            symbol = string.IsNullOrWhiteSpace(symbol) ? "NVDA" : symbol.Trim().ToUpper();
            Console.WriteLine($"[CapitalOS] Index START symbol={symbol}");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var dataTask = _stockMarketService.GetHomeMarketDataAsync(symbol);

                Console.WriteLine("[CapitalOS] Awaiting market data...");
                var completed = await Task.WhenAny(dataTask, Task.Delay(8000, cts.Token));

                if (completed == dataTask)
                {
                    Console.WriteLine("[CapitalOS] Market data returned OK");
                    return View(await dataTask);
                }

                Console.WriteLine("[CapitalOS] Market data TIMED OUT after 8s — returning fallback");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CapitalOS] Index EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }

            ViewBag.ErrorMessage = "Live market data is currently unavailable.";
            return View(new MarketHomeViewModel
            {
                FeaturedSymbol = symbol,
                FeaturedCompanyName = "Market data unavailable",
                CurrentPrice = 0,
                ChangePercent = 0,
                ChartPoints = new List<StockChartPoint>()
            });
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
