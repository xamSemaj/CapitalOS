using CapitalOS.Models;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace CapitalOS.Services
{
    public class AlphaVantageStockMarketService : IStockMarketService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AlphaVantageStockMarketService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<MarketHomeViewModel> GetHomeMarketDataAsync(string symbol)
        {
            var apiKey = _configuration["AlphaVantage:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Alpha Vantage API Key is missing..");

            }

            var url =
               $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to fetch stock market data.");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (!root.TryGetProperty("Time Series (Daily)", out var timeseries))
            {
                throw new Exception("Could not find daily time series data in API response. ");
            }

            var chartPoints = new List<StockChartPoint>();

            foreach (var day in timeseries.EnumerateObject().Take(30).Reverse())
            {
                var date = day.Name;
                var values = day.Value;

                var closeText = values.GetProperty("4. close").GetString();


                if (decimal.TryParse(closeText, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var closePrice))
                {
                    chartPoints.Add(new StockChartPoint
                    {

                        Date = DateTime.Parse(date).ToString("dd MMM"),
                        Price = closePrice
                    });
                }
            }


            var currentPrice = chartPoints.LastOrDefault()?.Price ?? 0;

            var previousPrice = chartPoints.Count >= 2
                ? chartPoints[^2].Price : currentPrice;

            var changePercent = previousPrice == 0
                ? 0
                : ((currentPrice - previousPrice) / previousPrice) * 100;

            return new MarketHomeViewModel
            {
                FeaturedSymbol = symbol.ToUpper(),
                FeaturedCompanyName = GetPlaceHolderCompanyName(symbol),
                CurrentPrice = currentPrice,
                ChangePercent = Math.Round(changePercent, 2),
                ChartPoints = chartPoints

            };


        }


        public async Task<StockQuoteViewModel> GetLatestQuoteAsync(string symbol)
        {
            symbol = string.IsNullOrWhiteSpace(symbol)
                ? "NVDA"
                : symbol.Trim().ToUpper();

            var apiKey = _configuration["AlphaVantage:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Alpha Vantage API key is missing.");
            }

            var url =
                $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch latest quote. Status code: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("Error Message", out var errorMessage))
            {
                throw new Exception(errorMessage.GetString() ?? "Alpha Vantage returned an error.");
            }

            if (root.TryGetProperty("Note", out var note))
            {
                throw new Exception(note.GetString() ?? "Alpha Vantage API limit reached.");
            }

            if (root.TryGetProperty("Information", out var information))
            {
                throw new Exception(information.GetString() ?? "Alpha Vantage returned an information message.");
            }

            if (!root.TryGetProperty("Global Quote", out var globalQuote))
            {
                throw new Exception("Could not find 'Global Quote' in the Alpha Vantage response.");
            }

            var priceText = globalQuote.GetProperty("05. price").GetString();
            var changeText = globalQuote.GetProperty("09. change").GetString();
            var changePercentText = globalQuote.GetProperty("10. change percent").GetString();

            changePercentText = changePercentText?.Replace("%", "");

            decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
            decimal.TryParse(changeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var change);
            decimal.TryParse(changePercentText, NumberStyles.Any, CultureInfo.InvariantCulture, out var changePercent);

            return new StockQuoteViewModel
            {
                Symbol = symbol,
                Price = price,
                Change = change,
                ChangePercent = changePercent,
                FetchedAt = DateTime.UtcNow
            };
        }

        public async Task<StockSearchViewModel> GetStockDiscoveryAsync(string? query)
        {
            var model = new StockSearchViewModel
            {
                Query = query
            };

            var apiKey = _configuration["AlphaVantage:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Alpha Vantage API key is missing.");
            }

            var moversUrl =
               $"https://www.alphavantage.co/query?function=TOP_GAINERS_LOSERS&apikey={apiKey}";

            var moversResponse = await _httpClient.GetAsync(moversUrl);

            if (!moversResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch market movers. Status code {moversResponse}");
            }

            var moversJson = await moversResponse.Content.ReadAsStringAsync();

            using (var document = JsonDocument.Parse(moversJson))
            {
                var root = document.RootElement;

                if (root.TryGetProperty("Note", out var note))
                {
                    throw new Exception(note.GetString() ?? "Alpha Vantage API limit Reached.");
                }

                if (root.TryGetProperty("top_gainers", out var topGainers))
                {
                    model.TopGainers = ParseMoverList(topGainers).Take(6).ToList();
                }

                if (root.TryGetProperty("top_losers", out var topLosers))
                {
                    model.TopLosers = ParseMoverList(topLosers).Take(6).ToList();

                }

                if (root.TryGetProperty("most_actively_traded", out var mostActive))
                {
                    model.MostActive = ParseMoverList(mostActive).Take(6).ToList();
                }
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                model.SearchResults = await SearchSymbolsAsync(query);
            }
            return model;

        }

        private List<StockDiscoveryItem> ParseMoverList(JsonElement items)
        {
            var results = new List<StockDiscoveryItem>();

            foreach (var item in items.EnumerateArray())
            {
                var ticker = item.GetProperty("ticker").GetString() ?? "";
                var priceText = item.GetProperty("price").GetString() ?? "0";
                var changePercentText = item.GetProperty("change_percentage").GetString() ?? "0";

                changePercentText = changePercentText.Replace("%", "");

                decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                decimal.TryParse(changePercentText, NumberStyles.Any, CultureInfo.InvariantCulture, out var changePercent);

                results.Add(new StockDiscoveryItem
                {
                    Symbol = ticker,
                    CompanyName = ticker,
                    Price = price,
                    ChangePercent = changePercent,
                    Sector = "Market mover"
                });

            }
            return results;
        }



        private async Task<List<StockDiscoveryItem>> SearchSymbolsAsync(string query)
        {
            var apiKey = _configuration["AlphaVantage:ApiKey"];

            var url =
                $"https://www.alphavantage.co/query?function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(query)}&apikey={apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to search symbols. Status code: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("Note", out var note))
            {
                throw new Exception(note.GetString() ?? "Alpha Vantage API limit reached.");
            }

            if (root.TryGetProperty("Information", out var information))
            {
                throw new Exception(information.GetString() ?? "Alpha Vantage returned an information message.");
            }

            if (!root.TryGetProperty("bestMatches", out var matches))
            {
                return new List<StockDiscoveryItem>();
            }

            var results = new List<StockDiscoveryItem>();

            foreach (var match in matches.EnumerateArray())
            {
                var symbol = match.GetProperty("1. symbol").GetString() ?? "";
                var name = match.GetProperty("2. name").GetString() ?? "";
                var region = match.GetProperty("4. region").GetString() ?? "";
                var currency = match.GetProperty("8. currency").GetString() ?? "";

                results.Add(new StockDiscoveryItem
                {
                    Symbol = symbol,
                    CompanyName = name,
                    Price = 0,
                    ChangePercent = 0,
                    Sector = $"{region} · {currency}"
                });
            }

            return results;
        }



        private string GetPlaceHolderCompanyName(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "NVDA" => "NVIDIA Corporation",
                "MSFT" => "Microsoft Corporation",
                "AAPL" => "Apple Inc.",
                "AMD" => "Advanced Micro Devices",
                "TSLA" => "Tesla Inc.",
                _ => "Selected Company"
            };
        }
    }
}

