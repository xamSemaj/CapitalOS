using CapitalOS.Services;
using Microsoft.AspNetCore.Mvc;

namespace CapitalOS.Controllers
{
    [Route("Market")]
    public class MarketController : Controller
    {
        private readonly IStockMarketService _stockMarketService;

        public MarketController(IStockMarketService stockMarketService)
        {
            _stockMarketService = stockMarketService;
        }

        [HttpGet("Quote")]
        public async Task<IActionResult> Quote(string symbol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return BadRequest(new { error = "Symbol is required." });
                }

                var quote = await _stockMarketService.GetLatestQuoteAsync(symbol);

                return Json(new
                {
                    symbol = quote.Symbol,
                    price = quote.Price,
                    change = quote.Change,
                    changePercent = quote.ChangePercent,
                    fetchedAt = quote.FetchedAt.ToString("HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }

        }
    }
}
