namespace CurrencyApp.Core.Models
{
    public class CurrencyAverageResult
    {
        public string Currency { get; set; } = string.Empty;
        public decimal AverageRate { get; set; }
        public int SampleCount { get; set; }
    }
}