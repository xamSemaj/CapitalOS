namespace CapitalOS.Models
{
    public class MarketHomeViewModel
    {
        public string FeaturedSymbol { get; set; } = "";
        public string FeaturedCompanyName { get; set; } = "";
        public decimal CurrentPrice { get; set; }
        public decimal ChangePercent { get; set; }

        public List<StockChartPoint> ChartPoints { get; set; } = new();

    }
}
