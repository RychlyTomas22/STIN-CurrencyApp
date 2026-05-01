using System.Text.Json.Serialization;

namespace CurrencyApp.Api.Models.ExchangeRateHost
{
    public class TimeframeRatesResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("timeframe")]
        public bool Timeframe { get; set; }

        [JsonPropertyName("start_date")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("end_date")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("quotes")]
        public Dictionary<string, Dictionary<string, decimal>> Quotes { get; set; } = new();
    }
}