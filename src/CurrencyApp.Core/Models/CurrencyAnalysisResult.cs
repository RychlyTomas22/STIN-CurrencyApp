namespace CurrencyApp.Core.Models
{
    public class CurrencyAnalysisResult
    {
        public string BaseCurrency { get; set; } = string.Empty;
        public DateOnly CurrentDate { get; set; }
        public ExchangeRateValue? StrongestCurrency { get; set; }
        public ExchangeRateValue? WeakestCurrency { get; set; }
        public List<CurrencyAverageResult> AverageRates { get; set; } = new();
    }
}