namespace CurrencyApp.Api.Configuration
{
    public class ExchangeRateHostOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 10;
        public string AccessKey { get; set; } = string.Empty;
    }
}