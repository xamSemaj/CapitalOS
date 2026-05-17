namespace CapitalOS.Models
{
    public class MarketIndexItem
    {
        public string Symbol { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
        public List<StockChartPoint> ChartPoints { get; set; } = new();

        public bool IsPositive => ChangePercent >= 0;
    }
}
