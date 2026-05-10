using CurrencyApp.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CurrencyApp.Tests
{
    public class MockExchangeRateHostClientTests
    {
        [Fact]
        public async Task GetLiveRatesAsync_ShouldReturnRatesForSelectedCurrencies()
        {
            var client = CreateClient(mockShouldFail: false);

            var result = await client.GetLiveRatesAsync(
                currencies: new[] { "CZK", "EUR", "GBP" },
                source: "USD",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Equal("USD", result.Source);
            Assert.Contains("USDCZK", result.Quotes.Keys);
            Assert.Contains("USDEUR", result.Quotes.Keys);
            Assert.Contains("USDGBP", result.Quotes.Keys);

            Assert.Equal(23.100000m, result.Quotes["USDCZK"]);
            Assert.Equal(0.920000m, result.Quotes["USDEUR"]);
            Assert.Equal(0.790000m, result.Quotes["USDGBP"]);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldNormalizeCurrenciesAndIgnoreBaseCurrency()
        {
            var client = CreateClient(mockShouldFail: false);

            var result = await client.GetLiveRatesAsync(
                currencies: new[] { " czk ", "CZK", "usd", "", "   ", "eur" },
                source: "usd",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Equal("USD", result.Source);

            Assert.Contains("USDCZK", result.Quotes.Keys);
            Assert.Contains("USDEUR", result.Quotes.Keys);
            Assert.DoesNotContain("USDUSD", result.Quotes.Keys);

            Assert.Equal(2, result.Quotes.Count);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldUseUsd_WhenSourceIsMissing()
        {
            var client = CreateClient(mockShouldFail: false);

            var result = await client.GetLiveRatesAsync(
                currencies: new[] { "CZK" },
                source: null,
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Equal("USD", result.Source);
            Assert.Contains("USDCZK", result.Quotes.Keys);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldSkipUnknownCurrencies()
        {
            var client = CreateClient(mockShouldFail: false);

            var result = await client.GetLiveRatesAsync(
                currencies: new[] { "CZK", "XXX" },
                source: "USD",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Contains("USDCZK", result.Quotes.Keys);
            Assert.DoesNotContain("USDXXX", result.Quotes.Keys);
            Assert.Single(result.Quotes);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldReturnEmptyQuotes_WhenBaseCurrencyIsUnknown()
        {
            var client = CreateClient(mockShouldFail: false);

            var result = await client.GetLiveRatesAsync(
                currencies: new[] { "CZK", "EUR" },
                source: "XXX",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Equal("XXX", result.Source);
            Assert.Empty(result.Quotes);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ShouldReturnHistoricalRatesForSelectedDate()
        {
            var client = CreateClient(mockShouldFail: false);
            var date = new DateOnly(2025, 1, 1);

            var result = await client.GetHistoricalRatesAsync(
                date: date,
                currencies: new[] { "CZK", "EUR" },
                source: "USD",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.True(result.Historical);
            Assert.Equal("USD", result.Source);
            Assert.Equal("2025-01-01", result.Date);

            Assert.Contains("USDCZK", result.Quotes.Keys);
            Assert.Contains("USDEUR", result.Quotes.Keys);

            Assert.Equal(2, result.Quotes.Count);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ShouldApplyDeterministicDateMultiplier()
        {
            var client = CreateClient(mockShouldFail: false);
            var date = new DateOnly(2025, 1, 1);

            var result = await client.GetHistoricalRatesAsync(
                date: date,
                currencies: new[] { "CZK" },
                source: "USD",
                cancellationToken: default);

            Assert.True(result.Success);
            Assert.Contains("USDCZK", result.Quotes.Keys);

            // 2025-01-01 has day-of-year 1:
            // multiplier = 1 + (((1 % 7) - 3) * 0.002) = 0.996
            // 23.1 * 0.996 = 23.0076
            Assert.Equal(23.007600m, result.Quotes["USDCZK"]);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldThrowHttpRequestException_WhenMockFailureIsEnabled()
        {
            var client = CreateClient(mockShouldFail: true);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.GetLiveRatesAsync(
                    currencies: new[] { "CZK" },
                    source: "USD",
                    cancellationToken: default));

            Assert.Equal("Mock ExchangeRateHost failure.", exception.Message);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ShouldThrowHttpRequestException_WhenMockFailureIsEnabled()
        {
            var client = CreateClient(mockShouldFail: true);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                client.GetHistoricalRatesAsync(
                    date: new DateOnly(2025, 1, 1),
                    currencies: new[] { "CZK" },
                    source: "USD",
                    cancellationToken: default));

            Assert.Equal("Mock ExchangeRateHost failure.", exception.Message);
        }

        private static MockExchangeRateHostClient CreateClient(bool mockShouldFail)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ExchangeRateHost:MockShouldFail"] = mockShouldFail.ToString()
                })
                .Build();

            return new MockExchangeRateHostClient(
                NullLogger<MockExchangeRateHostClient>.Instance,
                configuration);
        }
    }
}