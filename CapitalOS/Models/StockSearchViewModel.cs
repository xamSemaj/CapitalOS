namespace CapitalOS.Models
{
    public class StockSearchViewModel
    {
        public string? Query { get; set; }

        public List<StockDiscoveryItem> TopGainers { get; set; } = new();
        public List<StockDiscoveryItem> TopLosers { get; set; } = new();
        public List<StockDiscoveryItem> MostActive { get; set; } = new();
        public List<StockDiscoveryItem> SearchResults { get; set; } = new();

    }
}
