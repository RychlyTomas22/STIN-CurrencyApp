using System.Text.Json.Serialization;

namespace CurrencyApp.Api.Models.ExchangeRateHost
{
    public class LiveRatesResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("quotes")]
        public Dictionary<string, decimal> Quotes { get; set; } = new();
    }
}