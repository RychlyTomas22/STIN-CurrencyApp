using System.Text.Json.Serialization;

namespace CurrencyApp.Api.Models.ExchangeRateHost
{
    public class ExchangeRateErrorResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public ExchangeRateErrorDetails? Error { get; set; }
    }

    public class ExchangeRateErrorDetails
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("info")]
        public string Info { get; set; } = string.Empty;
    }
}