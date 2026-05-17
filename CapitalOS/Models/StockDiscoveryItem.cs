namespace CapitalOS.Models
{
    public class StockDiscoveryItem
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
        public string Sector { get; set; } = "";
        public List<StockChartPoint> ChartPoints { get; set; } = new();
    }
}
