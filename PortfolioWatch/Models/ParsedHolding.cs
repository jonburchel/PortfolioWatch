namespace PortfolioWatch.Models
{
    public class ParsedHolding
    {
        public string AccountName { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public double Quantity { get; set; }
        public double Price { get; set; }
        public double Value { get; set; }
        public string RawText { get; set; } = "";
        public bool IsFund { get; set; }
        public string? SubstitutionMessage { get; set; }
    }
}
