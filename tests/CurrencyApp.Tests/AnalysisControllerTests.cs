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
                baseCurrency: "USD",
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
                baseCurrency: "USD",
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

            var mapper = new FakeExchangeRateResponseMapper
            {
                LiveSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>()
                }
            };

            var controller = CreateController(
                mapper: mapper,
                cacheService: cache);

            var result = await controller.Analyze(
                baseCurrency: "USD",
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
                baseCurrency: "USD",
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
                baseCurrency: "USD",
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
        public async Task Analyze_ShouldUseProvidedBaseCurrency_WhenCacheContainsRequestedData()
        {
            var currentSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "EUR",
                Date = new DateOnly(2025, 1, 3),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 25.1m },
                    new() { Currency = "USD", Rate = 1.08m }
                }
            };

            var historicalSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "EUR",
                Date = new DateOnly(2025, 1, 1),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 25.0m },
                    new() { Currency = "USD", Rate = 1.07m }
                }
            };

            var cache = new FakeExchangeRateCacheService
            {
                LiveSnapshot = currentSnapshot
            };

            cache.HistoricalSnapshots[new DateOnly(2025, 1, 1)] = historicalSnapshot;

            var expectedResult = new CurrencyAnalysisResult
            {
                BaseCurrency = "EUR",
                CurrentDate = new DateOnly(2025, 1, 3),
                StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 25.1m },
                WeakestCurrency = new ExchangeRateValue { Currency = "USD", Rate = 1.08m },
                AverageRates = new List<CurrencyAverageResult>
                {
                    new() { Currency = "CZK", AverageRate = 25.0m, SampleCount = 1 },
                    new() { Currency = "USD", AverageRate = 1.07m, SampleCount = 1 }
                }
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = expectedResult
            };

            var controller = CreateController(
                cacheService: cache,
                analysisService: analysisService);

            var result = await controller.Analyze(
                baseCurrency: "EUR",
                currencies: "CZK,USD",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal("EUR", payload.BaseCurrency);
            Assert.Equal("CZK", payload.StrongestCurrency!.Currency);
            Assert.Equal("USD", payload.WeakestCurrency!.Currency);
        }

        [Fact]
        public async Task Analyze_ShouldPassProvidedBaseCurrencyToHostClient_WhenCacheMissOccurs()
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
                    BaseCurrency = "EUR",
                    Date = new DateOnly(2025, 1, 3),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 25.1m }
                    }
                },
                HistoricalSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "EUR",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 25.0m }
                    }
                }
            };

            var expectedResult = new CurrencyAnalysisResult
            {
                BaseCurrency = "EUR",
                CurrentDate = new DateOnly(2025, 1, 3),
                StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 25.1m },
                WeakestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 25.1m },
                AverageRates = new List<CurrencyAverageResult>
                {
                    new() { Currency = "CZK", AverageRate = 25.0m, SampleCount = 1 }
                }
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = expectedResult
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                analysisService: analysisService,
                cacheService: new FakeExchangeRateCacheService());

            var result = await controller.Analyze(
                baseCurrency: "EUR",
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal("EUR", hostClient.LastLiveSource);
            Assert.Equal("EUR", hostClient.LastHistoricalSource);
        }

        [Fact]
        public async Task Analyze_ShouldFallbackToUsd_WhenBaseCurrencyIsMissing()
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

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = new CurrencyAnalysisResult
                {
                    BaseCurrency = "USD",
                    CurrentDate = new DateOnly(2025, 1, 3),
                    StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                    WeakestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                    AverageRates = new List<CurrencyAverageResult>
                    {
                        new() { Currency = "CZK", AverageRate = 24.3m, SampleCount = 1 }
                    }
                }
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                analysisService: analysisService,
                cacheService: new FakeExchangeRateCacheService());

            var result = await controller.Analyze(
                baseCurrency: null,
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal("USD", hostClient.LastLiveSource);
            Assert.Equal("USD", hostClient.LastHistoricalSource);
        }

        [Fact]
        public async Task Analyze_ShouldFetchFreshLiveSnapshot_WhenCachedLiveSnapshotMissesRequestedCurrency()
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
                        new() { Currency = "CZK", Rate = 24.5m },
                        new() { Currency = "EUR", Rate = 0.92m }
                    }
                },
                HistoricalSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m },
                        new() { Currency = "EUR", Rate = 0.91m }
                    }
                }
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = new CurrencyAnalysisResult
                {
                    BaseCurrency = "USD",
                    CurrentDate = new DateOnly(2025, 1, 3),
                    StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                    WeakestCurrency = new ExchangeRateValue { Currency = "EUR", Rate = 0.92m },
                    AverageRates = new List<CurrencyAverageResult>
                    {
                        new() { Currency = "CZK", AverageRate = 24.3m, SampleCount = 1 },
                        new() { Currency = "EUR", AverageRate = 0.91m, SampleCount = 1 }
                    }
                }
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                analysisService: analysisService,
                cacheService: cache);

            var result = await controller.Analyze(
                baseCurrency: "USD",
                currencies: "CZK,EUR",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal(1, hostClient.LiveCallCount);
            Assert.NotNull(cache.SavedLiveSnapshot);
        }

        [Fact]
        public async Task Analyze_ShouldFetchFreshHistoricalSnapshot_WhenCachedHistoricalSnapshotMissesRequestedCurrency()
        {
            var cache = new FakeExchangeRateCacheService
            {
                LiveSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 3),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m },
                        new() { Currency = "EUR", Rate = 0.92m }
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

            var hostClient = new FakeExchangeRateHostClient
            {
                HistoricalResponse = new HistoricalRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                HistoricalSnapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "USD",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m },
                        new() { Currency = "EUR", Rate = 0.91m }
                    }
                }
            };

            var analysisService = new FakeCurrencyAnalysisService
            {
                Result = new CurrencyAnalysisResult
                {
                    BaseCurrency = "USD",
                    CurrentDate = new DateOnly(2025, 1, 3),
                    StrongestCurrency = new ExchangeRateValue { Currency = "CZK", Rate = 24.5m },
                    WeakestCurrency = new ExchangeRateValue { Currency = "EUR", Rate = 0.92m },
                    AverageRates = new List<CurrencyAverageResult>
                    {
                        new() { Currency = "CZK", AverageRate = 24.3m, SampleCount = 1 },
                        new() { Currency = "EUR", AverageRate = 0.91m, SampleCount = 1 }
                    }
                }
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                analysisService: analysisService,
                cacheService: cache);

            var result = await controller.Analyze(
                baseCurrency: "USD",
                currencies: "CZK,EUR",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal(0, hostClient.LiveCallCount);
            Assert.Equal(1, hostClient.HistoricalCallCount);
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
                baseCurrency: "USD",
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldReturn502_WhenLiveRequestIsRateLimited()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveException = new HttpRequestException("ExchangeRateHost returned HTTP 429.")
            };

            var controller = CreateController(
                hostClient: hostClient,
                cacheService: new FakeExchangeRateCacheService());

            var result = await controller.Analyze(
                baseCurrency: "USD",
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task Analyze_ShouldIgnoreHistoricalSnapshot_WhenHostClientThrowsHttp429()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                HistoricalException = new HttpRequestException("ExchangeRateHost returned HTTP 429.")
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
                }
            };

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
                cacheService: new FakeExchangeRateCacheService());

            var result = await controller.Analyze(
                baseCurrency: "USD",
                currencies: "CZK",
                startDate: new DateOnly(2025, 1, 1),
                endDate: new DateOnly(2025, 1, 1),
                cancellationToken: default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<CurrencyAnalysisResult>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Empty(payload.AverageRates);
            Assert.Equal(1, hostClient.HistoricalCallCount);
        }

        [Fact]
        public async Task Analyze_ShouldReturn502_WhenLiveMapperThrowsInvalidOperationException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveResponse = new LiveRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                LiveException = new InvalidOperationException("invalid live data")
            };

            var controller = CreateController(
                hostClient: hostClient,
                mapper: mapper,
                cacheService: new FakeExchangeRateCacheService());

            var result = await controller.Analyze(
                baseCurrency: "USD",
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
                baseCurrency: "USD",
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
            public string? LastLiveSource { get; private set; }
            public string? LastHistoricalSource { get; private set; }

            public Task<LiveRatesResponse> GetLiveRatesAsync(
                IEnumerable<string> currencies,
                string? source = null,
                CancellationToken cancellationToken = default)
            {
                LiveCallCount++;
                LastLiveSource = source;

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
                LastHistoricalSource = source;

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