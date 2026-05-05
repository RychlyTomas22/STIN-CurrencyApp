using CurrencyApp.Core.Models;
using CurrencyApp.Core.Services;
using Xunit;

namespace CurrencyApp.Tests
{
    public class CurrencyAnalysisServiceTests
    {
        private readonly CurrencyAnalysisService _service = new();

        [Fact]
        public void Analyze_ShouldReturnStrongestAndWeakestCurrency_FromCurrentSnapshot()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.5m },
                    new() { Currency = "EUR", Rate = 0.85m },
                    new() { Currency = "GBP", Rate = 0.75m }
                }
            };

            var historicalSnapshots = new List<ExchangeRateSnapshot>
            {
                new()
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.4m },
                        new() { Currency = "EUR", Rate = 0.84m },
                        new() { Currency = "GBP", Rate = 0.74m }
                    }
                }
            };

            var result = _service.Analyze(currentSnapshot, historicalSnapshots);

            Assert.NotNull(result.StrongestCurrency);
            Assert.NotNull(result.WeakestCurrency);

            Assert.Equal("CZK", result.StrongestCurrency!.Currency);
            Assert.Equal(24.5m, result.StrongestCurrency.Rate);

            Assert.Equal("GBP", result.WeakestCurrency!.Currency);
            Assert.Equal(0.75m, result.WeakestCurrency.Rate);
        }

        [Fact]
        public void Analyze_ShouldCalculateAverageRates_FromHistoricalSnapshots()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.5m },
                    new() { Currency = "EUR", Rate = 0.85m }
                }
            };

            var historicalSnapshots = new List<ExchangeRateSnapshot>
            {
                new()
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m },
                        new() { Currency = "EUR", Rate = 0.84m }
                    }
                },
                new()
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 2),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m },
                        new() { Currency = "EUR", Rate = 0.86m }
                    }
                }
            };

            var result = _service.Analyze(currentSnapshot, historicalSnapshots);

            var czk = result.AverageRates.Single(x => x.Currency == "CZK");
            var eur = result.AverageRates.Single(x => x.Currency == "EUR");

            Assert.Equal(24.4m, czk.AverageRate);
            Assert.Equal(2, czk.SampleCount);

            Assert.Equal(0.85m, eur.AverageRate);
            Assert.Equal(2, eur.SampleCount);
        }

        [Fact]
        public void Analyze_ShouldIgnoreMissingHistoricalData()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.5m },
                    new() { Currency = "EUR", Rate = 0.85m }
                }
            };

            var historicalSnapshots = new List<ExchangeRateSnapshot>
            {
                new()
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m }
                    }
                },
                new()
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 2),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m },
                        new() { Currency = "EUR", Rate = 0.87m }
                    }
                }
            };

            var result = _service.Analyze(currentSnapshot, historicalSnapshots);

            var czk = result.AverageRates.Single(x => x.Currency == "CZK");
            var eur = result.AverageRates.Single(x => x.Currency == "EUR");

            Assert.Equal(24.4m, czk.AverageRate);
            Assert.Equal(2, czk.SampleCount);

            Assert.Equal(0.87m, eur.AverageRate);
            Assert.Equal(1, eur.SampleCount);
        }

        [Fact]
        public void Analyze_ShouldThrow_WhenCurrentSnapshotIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                _service.Analyze(null!, new List<ExchangeRateSnapshot>()));

            Assert.Equal("currentSnapshot", ex.ParamName);
        }

        [Fact]
        public void Analyze_ShouldThrow_WhenCurrentSnapshotHasNoRates()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>()
            };

            var ex = Assert.Throws<ArgumentException>(() =>
                _service.Analyze(currentSnapshot, new List<ExchangeRateSnapshot>()));

            Assert.Contains("Current snapshot must contain at least one rate", ex.Message);
        }

        [Fact]
        public void Analyze_ShouldThrow_WhenHistoricalSnapshotUsesDifferentBaseCurrency()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.5m }
                }
            };

            var historicalSnapshots = new List<ExchangeRateSnapshot>
            {
                new()
                {
                    BaseCurrency = "EUR",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m }
                    }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.Analyze(currentSnapshot, historicalSnapshots));

            Assert.Contains("same base currency", ex.Message);
        }
    }
}