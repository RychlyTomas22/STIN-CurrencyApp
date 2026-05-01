using CurrencyApp.Api.Models.ExchangeRateHost;
using CurrencyApp.Core.Models;

namespace CurrencyApp.Api.Mappings
{
    public class ExchangeRateResponseMapper : IExchangeRateResponseMapper
    {
        public ExchangeRateSnapshot MapLive(LiveRatesResponse response)
        {
            var date = response.Timestamp.HasValue
                ? DateOnly.FromDateTime(
                    DateTimeOffset.FromUnixTimeSeconds(response.Timestamp.Value).UtcDateTime)
                : DateOnly.FromDateTime(DateTime.UtcNow);

            return new ExchangeRateSnapshot
            {
                BaseCurrency = response.Source,
                Date = date,
                Rates = MapQuotes(response.Source, response.Quotes)
            };
        }

        public ExchangeRateSnapshot MapHistorical(HistoricalRatesResponse response)
        {
            if (!DateOnly.TryParse(response.Date, out var parsedDate))
            {
                throw new InvalidOperationException("Invalid historical response date.");
            }

            return new ExchangeRateSnapshot
            {
                BaseCurrency = response.Source,
                Date = parsedDate,
                Rates = MapQuotes(response.Source, response.Quotes)
            };
        }

        private List<ExchangeRateValue> MapQuotes(
            string sourceCurrency,
            Dictionary<string, decimal> quotes)
        {
            var result = new List<ExchangeRateValue>();

            foreach (var quote in quotes)
            {
                var targetCurrency = ExtractTargetCurrency(sourceCurrency, quote.Key);

                result.Add(new ExchangeRateValue
                {
                    Currency = targetCurrency,
                    Rate = quote.Value
                });
            }

            return result
                .OrderBy(x => x.Currency)
                .ToList();
        }

        private string ExtractTargetCurrency(string sourceCurrency, string quoteKey)
        {
            if (string.IsNullOrWhiteSpace(sourceCurrency))
            {
                throw new InvalidOperationException("Source currency is missing.");
            }

            if (string.IsNullOrWhiteSpace(quoteKey))
            {
                throw new InvalidOperationException("Quote key is missing.");
            }

            if (!quoteKey.StartsWith(sourceCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Quote key '{quoteKey}' does not start with source currency '{sourceCurrency}'.");
            }

            var targetCurrency = quoteKey[sourceCurrency.Length..];

            if (string.IsNullOrWhiteSpace(targetCurrency))
            {
                throw new InvalidOperationException(
                    $"Failed to extract target currency from quote key '{quoteKey}'.");
            }

            return targetCurrency.ToUpperInvariant();
        }
    }
}
