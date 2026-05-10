namespace CapitalOS.Models
{
    public class StockQuoteViewModel
    {
        public string Symbol { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public DateTime FetchedAt { get; set; }
    }
}
