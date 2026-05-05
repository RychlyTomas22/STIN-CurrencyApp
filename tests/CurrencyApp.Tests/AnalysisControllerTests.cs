using CurrencyApp.Api.Controllers;
using CurrencyApp.Api.Mappings;
using CurrencyApp.Api.Models.ExchangeRateHost;
using CurrencyApp.Api.Services;
using CurrencyApp.Core.Models;
using CurrencyApp.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CurrencyApp.Tests
{
    public class AnalysisControllerTests
    {
        [Fact]
        public async Task Analyze_ShouldReturnBadRequest_WhenCurrenciesAreEmpty()
        {
            var controller = CreateController();

            var result = await controller.Analyze(
                currencies: "",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldReturnBadRequest_WhenEndDateIsBeforeStartDate()
        {
            var controller = CreateController();

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 2),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldReturnBadRequest_WhenFilteredCurrentSnapshotHasNoRequestedCurrencies()
        {
            var cache = new FakeExchangeRateCacheService
            {
                LiveSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "EUR", Rate = 0.85m }
                    }
                }
            };

            var controller = CreateController(cacheService: cache);

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldReturnOk_WhenSnapshotsAreLoadedFromCache()
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

            var historicalSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 1),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.3m }
                }
            };

            var expectedResult = new CurrencyAnalysisResult
            {
                BaseCurrency = "USD",
                CurrentDate = new DateOnly(2025, 1, 3),
                StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                WeakestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                AverageRates = new List<CurrencyAverageResult>
                {
                    new() { Currency = "CZK", AverageRate = 24.3m, SampleCount = 1 }
                }
            };

            var cache = new FakeExchangeRateCacheService
            {
                LiveSnapshot = currentSnapshot
            };
            cache.HistoricalSnapshots[new DateOnly(2025, 1, 1)] = historicalSnapshot;

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = expectedResult
            };

            var hostClient = new FakeExchangeRateHostClient();

            var controller = CreateController(
                hostClient: hostClient,
                analysisService: analysisService,
                cacheService: cache);

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Equal("CZK", payload.StrongestCurrency!.Currency);
            Assert.Equal(0, hostClient.LiveCallCount);
            Assert.Equal(0, hostClient.HistoricalCallCount);
        }

        [Fact]
        public async Task Analyze_ShouldFetchAndCacheSnapshots_WhenCacheMissOccurs()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveResponse = new LiveRatesResponse(),
                HistoricalResponse = new HistoricalRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                LiveSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 3),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m }
                    }
                },
                HistoricalSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m }
                    }
                }
            };

            var cache = new FakeExchangeRateCacheService();

            var expectedResult = new CurrencyAnalysisResult
            {
                BaseCurrency = "USD",
                CurrentDate = new DateOnly(2025, 1, 3),
                StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                WeakestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                AverageRates = new List<CurrencyAverageResult>()
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = expectedResult
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                analysisService: analysisService,
                cacheService: cache);

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal(1, hostClient.LiveCallCount);
            Assert.Equal(1, hostClient.HistoricalCallCount);
            Assert.NotNull(cache.SavedLiveSnapshot);
            Assert.Single(cache.SavedHistoricalSnapshots);
        }

        [Fact]
        public async Task Analyze_ShouldReturn502_WhenHostClientThrowsHttpRequestException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveException = new HttpRequestException("boom")
            };

            var controller = CreateController(hostClient: hostClient);

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldReturn502_WhenAnalysisServiceThrowsInvalidOperationException()
        {
            var cache = new FakeExchangeRateCacheService
            {
                LiveSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 3),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m }
                    }
                }
            };
            cache.HistoricalSnapshots[new DateOnly(2025, 1, 1)] = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 1),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.3m }
                }
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Exception = new InvalidOperationException("bad data")
            };

            var controller = CreateController(
                cacheService: cache,
                analysisService: analysisService);

            var result = await controller.Analyze(
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        private static AnalysisController CreateController(
            FakeExchangeRateHostClient? hostClient = null,
            FakeExchangeRateResponseMapper? mapper = null,
            FakeCurrencyAnalysisService? analysisService = null,
            FakeExchangeRateCacheService? cacheService = null)
        {
            return new AnalysisController(
                hostClient ?? new FakeExchangeRateHostClient(),
                mapper ?? new FakeExchangeRateResponseMapper(),
                analysisService ?? new FakeCurrencyAnalysisService(),
                cacheService ?? new FakeExchangeRateCacheService(),
                NullLogger<AnalysisController>.Instance);
        }

        private sealed class FakeExchangeRateHostClient : IExchangeRateHostClient
        {
            public int LiveCallCount { get; private set; }
            public int HistoricalCallCount { get; private set; }
            public Exception? LiveException { get; set; }
            public Exception? HistoricalException { get; set; }
            public LiveRatesResponse LiveResponse { get; set; } = new();
            public HistoricalRatesResponse HistoricalResponse { get; set; } = new();

            public Task<LiveRatesResponse> GetLiveRatesAsync(
                IEnumerable<string> currencies,
                string? source = null,
                CancellationToken cancellationToken = default)
            {
                LiveCallCount++;

                if (LiveException is not null)
                {
                    throw LiveException;
                }

                return Task.FromResult(LiveResponse);
            }

            public Task<HistoricalRatesResponse> GetHistoricalRatesAsync(
                DateOnly date,
                IEnumerable<string> currencies,
                string? source = null,
                CancellationToken cancellationToken = default)
            {
                HistoricalCallCount++;

                if (HistoricalException is not null)
                {
                    throw HistoricalException;
                }

                return Task.FromResult(HistoricalResponse);
            }
        }

        private sealed class FakeExchangeRateResponseMapper : IExchangeRateResponseMapper
        {
            public ExchangeRateSnapshot LiveSnapshot { get; set; } = new();
            public ExchangeRateSnapshot HistoricalSnapshot { get; set; } = new();
            public Exception? LiveException { get; set; }
            public Exception? HistoricalException { get; set; }

            public ExchangeRateSnapshot MapLive(LiveRatesResponse response)
            {
                if (LiveException is not null)
                {
                    throw LiveException;
                }

                return LiveSnapshot;
            }

            public ExchangeRateSnapshot MapHistorical(HistoricalRatesResponse response)
            {
                if (HistoricalException is not null)
                {
                    throw HistoricalException;
                }

                return HistoricalSnapshot;
            }
        }

        private sealed class FakeCurrencyAnalysisService : ICurrencyAnalysisService
        {
            public CurrencyAnalysisResult Result { get; set; } = new();
            public Exception? Exception { get; set; }

            public CurrencyAnalysisResult Analyze(
                ExchangeRateSnapshot currentSnapshot,
                IEnumerable<ExchangeRateSnapshot> historicalSnapshots)
            {
                if (Exception is not null)
                {
                    throw Exception;
                }

                return Result;
            }
        }

        private sealed class FakeExchangeRateCacheService : IExchangeRateCacheService
        {
            public ExchangeRateSnapshot? LiveSnapshot { get; set; }
            public Dictionary<DateOnly, ExchangeRateSnapshot> HistoricalSnapshots { get; } = new();
            public ExchangeRateSnapshot? SavedLiveSnapshot { get; private set; }
            public List<ExchangeRateSnapshot> SavedHistoricalSnapshots { get; } = new();

            public Task<ExchangeRateSnapshot?> TryGetLiveSnapshotAsync(
                string baseCurrency,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LiveSnapshot);
            }

            public Task SaveLiveSnapshotAsync(
                ExchangeRateSnapshot snapshot,
                CancellationToken cancellationToken = default)
            {
                SavedLiveSnapshot = snapshot;
                return Task.CompletedTask;
            }

            public Task<ExchangeRateSnapshot?> TryGetHistoricalSnapshotAsync(
                string baseCurrency,
                DateOnly date,
                CancellationToken cancellationToken = default)
            {
                HistoricalSnapshots.TryGetValue(date, out var snapshot);
                return Task.FromResult(snapshot);
            }

            public Task SaveHistoricalSnapshotAsync(
                ExchangeRateSnapshot snapshot,
                CancellationToken cancellationToken = default)
            {
                SavedHistoricalSnapshots.Add(snapshot);
                return Task.CompletedTask;
            }
        }
    }
}