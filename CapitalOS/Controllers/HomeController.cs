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

    


        public IActionResult Index(string symbol = "NVDA")
        {
            symbol = string.IsNullOrWhiteSpace(symbol) ? "NVDA" : symbol.Trim().ToUpper();
            return View(new MarketHomeViewModel
            {
                FeaturedSymbol = symbol,
                FeaturedCompanyName = "",
                CurrentPrice = 0,
                ChangePercent = 0,
                ChartPoints = new List<StockChartPoint>()
            });
        }

        [HttpGet("/api/market/{symbol}")]
        public async Task<IActionResult> MarketData(string symbol = "NVDA")
        {
            symbol = string.IsNullOrWhiteSpace(symbol) ? "NVDA" : symbol.Trim().ToUpper();
            try
            {
                var data = await _stockMarketService.GetHomeMarketDataAsync(symbol);
                return Json(new
                {
                    symbol = data.FeaturedSymbol,
                    companyName = data.FeaturedCompanyName,
                    price = data.CurrentPrice,
                    changePercent = data.ChangePercent,
                    chartPoints = data.ChartPoints.Select(p => new { date = p.Date, price = p.Price })
                });
            }
            catch
            {
                return StatusCode(503);
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
