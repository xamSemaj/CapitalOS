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
                ViewBag.ErrorMessage = ex.Message;

                return View(new StockSearchViewModel
                {
                    Query = query
                });
            }
        }
    }
}
