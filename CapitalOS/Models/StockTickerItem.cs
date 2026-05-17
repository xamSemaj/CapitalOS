namespace CapitalOS.Models
{
    public class StockTickerItem
    {
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }

        public bool IsPositive => ChangePercent >= 0;
    }
}
