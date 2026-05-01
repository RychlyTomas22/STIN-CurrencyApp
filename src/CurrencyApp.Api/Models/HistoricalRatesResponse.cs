using System.Text.Json.Serialization;

namespace CurrencyApp.Api.Models.ExchangeRateHost
{
    public class HistoricalRatesResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("historical")]
        public bool Historical { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("quotes")]
        public Dictionary<string, decimal> Quotes { get; set; } = new();
    }
}