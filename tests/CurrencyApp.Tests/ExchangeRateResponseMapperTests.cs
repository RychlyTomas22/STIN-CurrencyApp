using CurrencyApp.Api.Mappings;
using CurrencyApp.Api.Models.ExchangeRateHost;
using Xunit;

namespace CurrencyApp.Tests
{
    public class ExchangeRateResponseMapperTests
    {
        private readonly ExchangeRateResponseMapper _mapper = new();

        [Fact]
        public void MapLive_ShouldReturnNormalizedSnapshot()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = 1735689600, // 2025-01-01 00:00:00 UTC
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
                {
                    { "USDEUR", 0.85m },
                    { "USDCZK", 24.50m }
                }
            };

            var result = _mapper.MapLive(response);

            Assert.Equal("USD", result.BaseCurrency);
            Assert.Equal(new DateOnly(2025, 1, 1), result.Date);
            Assert.Equal(2, result.Rates.Count);

            Assert.Equal("CZK", result.Rates[0].Currency);
            Assert.Equal(24.50m, result.Rates[0].Rate);

            Assert.Equal("EUR", result.Rates[1].Currency);
            Assert.Equal(0.85m, result.Rates[1].Rate);
        }

        [Fact]
        public void MapHistorical_ShouldReturnNormalizedSnapshot()
        {
            var response = new HistoricalRatesResponse
            {
                Success = true,
                Historical = true,
                Date = "2025-01-03",
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
                {
                    { "USDCZK", 24.30m }
                }
            };

            var result = _mapper.MapHistorical(response);

            Assert.Equal("USD", result.BaseCurrency);
            Assert.Equal(new DateOnly(2025, 1, 3), result.Date);
            Assert.Single(result.Rates);
            Assert.Equal("CZK", result.Rates[0].Currency);
            Assert.Equal(24.30m, result.Rates[0].Rate);
        }

        [Fact]
        public void MapHistorical_ShouldThrow_WhenDateIsInvalid()
        {
            var response = new HistoricalRatesResponse
            {
                Success = true,
                Historical = true,
                Date = "not-a-date",
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
                {
                    { "USDCZK", 24.30m }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _mapper.MapHistorical(response));

            Assert.Contains("Invalid historical response date", ex.Message);
        }

        [Fact]
        public void MapLive_ShouldThrow_WhenQuoteKeyDoesNotMatchSourceCurrency()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = 1735689600,
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
                {
                    { "EURCZK", 24.50m }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _mapper.MapLive(response));

            Assert.Contains("does not start with source currency", ex.Message);
        }

        [Fact]
        public void MapLive_ShouldUseCurrentUtcDate_WhenTimestampIsMissing()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = null,
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
        {
            { "USDCZK", 24.50m }
        }
            };

            var result = _mapper.MapLive(response);

            Assert.Equal("USD", result.BaseCurrency);
            Assert.Single(result.Rates);
            Assert.Equal("CZK", result.Rates[0].Currency);

            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            Assert.Equal(todayUtc, result.Date);
        }

        [Fact]
        public void MapLive_ShouldThrow_WhenSourceCurrencyIsMissing()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = 1735689600,
                Source = "",
                Quotes = new Dictionary<string, decimal>
        {
            { "USDCZK", 24.50m }
        }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _mapper.MapLive(response));

            Assert.Contains("Source currency is missing", ex.Message);
        }

        [Fact]
        public void MapLive_ShouldThrow_WhenQuoteKeyIsMissing()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = 1735689600,
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
        {
            { "", 24.50m }
        }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _mapper.MapLive(response));

            Assert.Contains("Quote key is missing", ex.Message);
        }

        [Fact]
        public void MapLive_ShouldThrow_WhenQuoteKeyHasNoTargetCurrency()
        {
            var response = new LiveRatesResponse
            {
                Success = true,
                Timestamp = 1735689600,
                Source = "USD",
                Quotes = new Dictionary<string, decimal>
        {
            { "USD", 1.0m }
        }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _mapper.MapLive(response));

            Assert.Contains("Failed to extract target currency", ex.Message);
        }

    }
}