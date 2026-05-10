using CurrencyApp.Api.Models.ExchangeRateHost;

namespace CurrencyApp.Api.Services
{
    public class MockExchangeRateHostClient : IExchangeRateHostClient
    {
        private static readonly Dictionary<string, decimal> UsdBasedRates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1.0000m,
            ["EUR"] = 0.9200m,
            ["CZK"] = 23.1000m,
            ["GBP"] = 0.7900m,
            ["JPY"] = 151.0000m,
            ["PLN"] = 3.9700m,
            ["CHF"] = 0.8800m
        };

        private readonly ILogger<MockExchangeRateHostClient> _logger;
        private readonly IConfiguration _configuration;

        public MockExchangeRateHostClient(
            ILogger<MockExchangeRateHostClient> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Task<LiveRatesResponse> GetLiveRatesAsync(
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfMockFailureIsEnabled();

            var baseCurrency = NormalizeCurrency(source, "USD");
            var selectedCurrencies = NormalizeCurrencies(currencies, baseCurrency);

            _logger.LogInformation(
                "Using mock live exchange rates for base currency {BaseCurrency}.",
                baseCurrency);

            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Source = baseCurrency,
                Quotes = BuildQuotes(baseCurrency, selectedCurrencies, multiplier: 1m)
            };

            return Task.FromResult(response);
        }

        public Task<HistoricalRatesResponse> GetHistoricalRatesAsync(
            DateOnly date,
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfMockFailureIsEnabled();

            var baseCurrency = NormalizeCurrency(source, "USD");
            var selectedCurrencies = NormalizeCurrencies(currencies, baseCurrency);

            _logger.LogInformation(
                "Using mock historical exchange rates for {Date} and base currency {BaseCurrency}.",
                date,
                baseCurrency);

            var response = new HistoricalRatesResponse
            {
                Success = true,
                Historical = true,
                Date = date.ToString("yyyy-MM-dd"),
                Source = baseCurrency,
                Quotes = BuildQuotes(baseCurrency, selectedCurrencies, GetDateMultiplier(date))
            };

            return Task.FromResult(response);
        }

        private static Dictionary<string, decimal> BuildQuotes(
            string baseCurrency,
            IEnumerable<string> selectedCurrencies,
            decimal multiplier)
        {
            var quotes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var targetCurrency in selectedCurrencies)
            {
                if (!UsdBasedRates.ContainsKey(baseCurrency) ||
                    !UsdBasedRates.ContainsKey(targetCurrency))
                {
                    continue;
                }

                var rate = UsdBasedRates[targetCurrency] / UsdBasedRates[baseCurrency];
                rate *= multiplier;

                var quoteKey = $"{baseCurrency}{targetCurrency}";

                quotes[quoteKey] = decimal.Round(rate, 6);
            }

            return quotes;
        }

        private static decimal GetDateMultiplier(DateOnly date)
        {
            var variation = ((date.DayOfYear % 7) - 3) * 0.002m;
            return 1m + variation;
        }

        private static string NormalizeCurrency(string? currency, string fallback)
        {
            return string.IsNullOrWhiteSpace(currency)
                ? fallback
                : currency.Trim().ToUpperInvariant();
        }

        private static List<string> NormalizeCurrencies(
            IEnumerable<string> currencies,
            string baseCurrency)
        {
            return currencies
                .Where(currency => !string.IsNullOrWhiteSpace(currency))
                .Select(currency => currency.Trim().ToUpperInvariant())
                .Where(currency => !string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ThrowIfMockFailureIsEnabled()
        {
            var shouldFail = _configuration.GetValue<bool>("ExchangeRateHost:MockShouldFail");

            if (shouldFail)
            {
                throw new HttpRequestException("Mock ExchangeRateHost failure.");
            }
        }
    }
}