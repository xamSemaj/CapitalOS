using CapitalOS.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text.Json;

namespace CapitalOS.Services
{
    public class YahooStockMarketService : IStockMarketService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public YahooStockMarketService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<MarketHomeViewModel> GetHomeMarketDataAsync(string symbol)
        {
            symbol = CleanSymbol(symbol);

            var cacheKey = $"yahoo_home_market_data_{symbol}";

            if (_cache.TryGetValue(cacheKey, out MarketHomeViewModel? cachedModel) && cachedModel != null)
            {
                return cachedModel;
            }

            var url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1mo&interval=1d";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 CapitalOS/1.0");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Yahoo Finance request failed. Status code: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var result = GetFirstChartResult(root);

            var meta = result.GetProperty("meta");
            var quote = result.GetProperty("indicators").GetProperty("quote")[0];

            var timestamps = result.GetProperty("timestamp");
            var closes = quote.GetProperty("close");

            var chartPoints = new List<StockChartPoint>();

            for (var i = 0; i < timestamps.GetArrayLength(); i++)
            {
                if (i >= closes.GetArrayLength())
                {
                    break;
                }

                if (closes[i].ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                var unixTime = timestamps[i].GetInt64();
                var date = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;

                var closePrice = closes[i].GetDecimal();

                chartPoints.Add(new StockChartPoint
                {
                    Date = date.ToString("dd MMM"),
                    Price = closePrice
                });
            }

            var currentPrice = GetDecimalFromMeta(meta, "regularMarketPrice");

            if (currentPrice == 0)
            {
                currentPrice = chartPoints.LastOrDefault()?.Price ?? 0;
            }

            var previousClose = GetDecimalFromMeta(meta, "previousClose");

            if (previousClose == 0 && chartPoints.Count >= 2)
            {
                previousClose = chartPoints[^2].Price;
            }

            var changePercent = previousClose == 0
                ? 0
                : ((currentPrice - previousClose) / previousClose) * 100;

            var currency = GetStringFromMeta(meta, "currency");
            var exchangeName = GetStringFromMeta(meta, "exchangeName");
            var longName = GetStringFromMeta(meta, "longName");
            var shortName = GetStringFromMeta(meta, "shortName");

            var companyName = !string.IsNullOrWhiteSpace(longName)
                ? longName
                : !string.IsNullOrWhiteSpace(shortName)
                    ? shortName
                    : GetPlaceHolderCompanyName(symbol);

            var model = new MarketHomeViewModel
            {
                FeaturedSymbol = symbol,
                FeaturedCompanyName = companyName,
                CurrentPrice = currentPrice,
                ChangePercent = Math.Round(changePercent, 2),
                ChartPoints = chartPoints,
                TickerItems = await GetTickerItemsCoreAsync()
            };

            _cache.Set(cacheKey, model, TimeSpan.FromMinutes(15));
            model.TickerItems = await GetTickerItemsCoreAsync();
            return model;
        }

        public async Task<StockQuoteViewModel> GetLatestQuoteAsync(string symbol)
        {
            symbol = CleanSymbol(symbol);

            var cacheKey = $"yahoo_latest_quote_{symbol}";

            if (_cache.TryGetValue(cacheKey, out StockQuoteViewModel? cachedQuote) && cachedQuote != null)
            {
                return cachedQuote;
            }

            var url =
                $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1m";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 CapitalOS/1.0");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Yahoo Finance quote request failed. Status code: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var result = GetFirstChartResult(root);
            var meta = result.GetProperty("meta");

            var price = GetDecimalFromMeta(meta, "regularMarketPrice");
            var previousClose = GetDecimalFromMeta(meta, "previousClose");

            var change = previousClose == 0 ? 0 : price - previousClose;
            var changePercent = previousClose == 0 ? 0 : (change / previousClose) * 100;

            var quote = new StockQuoteViewModel
            {
                Symbol = symbol,
                Price = price,
                Change = Math.Round(change, 2),
                ChangePercent = Math.Round(changePercent, 2),
                FetchedAt = DateTime.UtcNow
            };

            _cache.Set(cacheKey, quote, TimeSpan.FromMinutes(1));

            return quote;
        }

        public async Task<StockSearchViewModel> GetStockDiscoveryAsync(string? query)
        {
            var model = new StockSearchViewModel
            {
                Query = query,
                TickerItems = await GetTickerItemsCoreAsync(),
                MarketIndices = await GetMarketIndicesAsync()
            };

            var popularSymbols = new[]
            {
        "NVDA", "MSFT", "AAPL", "AMD", "TSLA", "AMZN", "META", "GOOGL", "PLTR"
    };

            var items = new List<StockDiscoveryItem>();

            foreach (var symbol in popularSymbols)
            {
                try
                {
                    var homeData = await GetHomeMarketDataAsync(symbol);

                    items.Add(new StockDiscoveryItem
                    {
                        Symbol = homeData.FeaturedSymbol,
                        CompanyName = homeData.FeaturedCompanyName,
                        Price = homeData.CurrentPrice,
                        ChangePercent = homeData.ChangePercent,
                        Sector = GetSectorPlaceholder(homeData.FeaturedSymbol),
                        ChartPoints = homeData.ChartPoints
                    });
                }
                catch
                {
                    // Do not kill the whole Markets page because one ticker failed.
                }
            }

            model.TopGainers = items
                .OrderByDescending(x => x.ChangePercent)
                .Take(6)
                .ToList();

            model.TopLosers = items
                .OrderBy(x => x.ChangePercent)
                .Take(6)
                .ToList();

            model.MostActive = items
                .Take(6)
                .ToList();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var cleanedQuery = query.Trim().ToUpper();

                model.SearchResults = items
                    .Where(x =>
                        x.Symbol.Contains(cleanedQuery) ||
                        x.CompanyName.ToUpper().Contains(cleanedQuery))
                    .ToList();

                if (!model.SearchResults.Any())
                {
                    try
                    {
                        var homeData = await GetHomeMarketDataAsync(cleanedQuery);

                        model.SearchResults.Add(new StockDiscoveryItem
                        {
                            Symbol = homeData.FeaturedSymbol,
                            CompanyName = homeData.FeaturedCompanyName,
                            Price = homeData.CurrentPrice,
                            ChangePercent = homeData.ChangePercent,
                            Sector = "Search result",
                            ChartPoints = homeData.ChartPoints
                        });
                    }
                    catch
                    {
                        // Leave results empty if Yahoo cannot find it.
                    }
                }
            }

            return model;
        }

        private static string CleanSymbol(string symbol)
        {
            return string.IsNullOrWhiteSpace(symbol)
                ? "NVDA"
                : symbol.Trim().ToUpper();
        }

        private static JsonElement GetFirstChartResult(JsonElement root)
        {
            var chart = root.GetProperty("chart");

            if (chart.TryGetProperty("error", out var error) &&
                error.ValueKind != JsonValueKind.Null)
            {
                throw new Exception("Yahoo Finance returned an error for this symbol.");
            }

            var results = chart.GetProperty("result");

            if (results.ValueKind == JsonValueKind.Null || results.GetArrayLength() == 0)
            {
                throw new Exception("Yahoo Finance returned no chart results.");
            }

            return results[0];
        }

        private static decimal GetDecimalFromMeta(JsonElement meta, string propertyName)
        {
            if (!meta.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDecimal();
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static string GetStringFromMeta(JsonElement meta, string propertyName)
        {
            if (!meta.TryGetProperty(propertyName, out var value))
            {
                return "";
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
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
                "AMZN" => "Amazon.com Inc.",
                "META" => "Meta Platforms Inc.",
                "GOOGL" => "Alphabet Inc.",
                "PLTR" => "Palantir Technologies Inc.",
                _ => "Selected Company"
            };
        }

        private string GetSectorPlaceholder(string symbol)
        {
            return symbol.ToUpper() switch
            {
                "NVDA" => "Semiconductors",
                "AMD" => "Semiconductors",
                "MSFT" => "Technology",
                "AAPL" => "Technology",
                "TSLA" => "Automotive",
                "AMZN" => "Consumer",
                "META" => "Communication",
                "GOOGL" => "Communication",
                "PLTR" => "Software",
                _ => "Market"
            };
        }

        private async Task<List<StockTickerItem>> GetTickerItemsAsync()
        {
            var cacheKey = "yahoo_real_ticker_items";

            if (_cache.TryGetValue(cacheKey, out List<StockTickerItem>? cachedTicker) &&
                cachedTicker != null)
            {
                return cachedTicker;
            }

            var tickerSymbols = new[]
            {
        "AAPL", "NVDA", "MSFT", "TSLA", "GOOGL",
        "META", "AMZN", "NFLX", "AMD", "JPM"
    };

            var tickerItems = new List<StockTickerItem>();

            foreach (var symbol in tickerSymbols)
            {
                try
                {
                    var data = await GetHomeMarketDataAsync(symbol);

                    tickerItems.Add(new StockTickerItem
                    {
                        Symbol = data.FeaturedSymbol,
                        CompanyName = data.FeaturedCompanyName,
                        Price = data.CurrentPrice,
                        ChangePercent = data.ChangePercent
                    });
                }
                catch
                {
                    // If one ticker fails, don't kill the entire page.
                }
            }

            _cache.Set(cacheKey, tickerItems, TimeSpan.FromMinutes(5));

            return tickerItems;
        }

        private async Task<List<StockTickerItem>> GetTickerItemsCoreAsync()
        {
            var cacheKey = "yahoo_real_ticker_items";

            if (_cache.TryGetValue(cacheKey, out List<StockTickerItem>? cachedTicker) &&
                cachedTicker != null)
            {
                return cachedTicker;
            }

            var tickerSymbols = new[]
            {
        "AAPL", "NVDA", "MSFT", "TSLA", "GOOGL",
        "META", "AMZN", "NFLX", "AMD", "JPM"
    };

            var tickerItems = new List<StockTickerItem>();

            foreach (var symbol in tickerSymbols)
            {
                try
                {
                    var cleanSymbol = CleanSymbol(symbol);

                    var url =
                        $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(cleanSymbol)}?range=5d&interval=1d";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 CapitalOS/1.0");

                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();

                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;

                    var result = GetFirstChartResult(root);
                    var meta = result.GetProperty("meta");

                    var currentPrice = GetDecimalFromMeta(meta, "regularMarketPrice");
                    var previousClose = GetDecimalFromMeta(meta, "previousClose");

                    var changePercent = previousClose == 0
                        ? 0
                        : ((currentPrice - previousClose) / previousClose) * 100;

                    var longName = GetStringFromMeta(meta, "longName");
                    var shortName = GetStringFromMeta(meta, "shortName");

                    var companyName = !string.IsNullOrWhiteSpace(longName)
                        ? longName
                        : !string.IsNullOrWhiteSpace(shortName)
                            ? shortName
                            : GetPlaceHolderCompanyName(cleanSymbol);

                    tickerItems.Add(new StockTickerItem
                    {
                        Symbol = cleanSymbol,
                        CompanyName = companyName,
                        Price = currentPrice,
                        ChangePercent = Math.Round(changePercent, 2)
                    });
                }
                catch
                {
                    // Ignore single ticker failure.
                }
            }

            _cache.Set(cacheKey, tickerItems, TimeSpan.FromMinutes(5));

            return tickerItems;
        }

        private async Task<List<MarketIndexItem>> GetMarketIndicesAsync()
        {
            var cacheKey = "yahoo_market_indices";

            if (_cache.TryGetValue(cacheKey, out List<MarketIndexItem>? cachedIndices) &&
                cachedIndices != null)
            {
                return cachedIndices;
            }

            var indexSymbols = new[]
            {
        new { Symbol = "^GSPC", Label = "S&P 500" },
        new { Symbol = "^IXIC", Label = "NASDAQ" },
        new { Symbol = "^DJI",  Label = "DOW JONES" },
        new { Symbol = "^VIX",  Label = "VIX" }
    };

            var indices = new List<MarketIndexItem>();

            foreach (var index in indexSymbols)
            {
                try
                {
                    var data = await GetHomeMarketDataAsync(index.Symbol);

                    indices.Add(new MarketIndexItem
                    {
                        Symbol = data.FeaturedSymbol,
                        Label = index.Label,
                        Price = data.CurrentPrice,
                        ChangePercent = data.ChangePercent,
                        ChartPoints = data.ChartPoints
                    });
                }
                catch
                {
                    // No fake fallback. If Yahoo fails, this index is simply not shown.
                }
            }

            _cache.Set(cacheKey, indices, TimeSpan.FromMinutes(15));

            return indices;
        }

    }
}

