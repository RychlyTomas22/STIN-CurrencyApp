using CurrencyApp.Api.Controllers;
using CurrencyApp.Api.Mappings;
using CurrencyApp.Api.Models.ExchangeRateHost;
using CurrencyApp.Api.Services;
using CurrencyApp.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CurrencyApp.Tests
{
    public class RatesControllerTests
    {
        [Fact]
        public async Task GetLiveRates_ShouldReturnOk_WhenRequestIsValid()
        {
            var expectedSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = "USD",
                Date = new DateOnly(2025, 1, 1),
                Rates = new List<ExchangeRateValue>
                {
                    new() { Currency = "CZK", Rate = 24.5m }
                }
            };

            var hostClient = new FakeExchangeRateHostClient
            {
                LiveResponse = new LiveRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                LiveSnapshot = expectedSnapshot
            };

            var controller = CreateController(hostClient, mapper);

            var result = await controller.GetLiveRates("CZK", default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<ExchangeRateSnapshot>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Single(payload.Rates);
            Assert.Equal("CZK", payload.Rates[0].Currency);
            Assert.Equal(1, hostClient.LiveCallCount);
        }

        [Fact]
        public async Task GetLiveRates_ShouldReturnBadRequest_WhenCurrenciesAreEmpty()
        {
            var controller = CreateController(
                new FakeExchangeRateHostClient(),
                new FakeExchangeRateResponseMapper());

            var result = await controller.GetLiveRates("", default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task GetLiveRates_ShouldReturn502_WhenMapperThrowsInvalidOperationException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveResponse = new LiveRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                LiveException = new InvalidOperationException("bad live data")
            };

            var controller = CreateController(hostClient, mapper);

            var result = await controller.GetLiveRates("CZK", default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetLiveRates_ShouldReturn502_WhenHostClientThrowsHttpRequestException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                LiveException = new HttpRequestException("boom")
            };

            var controller = CreateController(hostClient, new FakeExchangeRateResponseMapper());

            var result = await controller.GetLiveRates("CZK", default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetHistoricalRates_ShouldReturnOk_WhenRequestIsValid()
        {
            var expectedSnapshot = new ExchangeRateSnapshot
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
                HistoricalSnapshot = expectedSnapshot
            };

            var controller = CreateController(hostClient, mapper);

            var result = await controller.GetHistoricalRates(
                new DateOnly(2025, 1, 1),
                "CZK",
                default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<ExchangeRateSnapshot>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Single(payload.Rates);
            Assert.Equal("CZK", payload.Rates[0].Currency);
            Assert.Equal(1, hostClient.HistoricalCallCount);
        }

        [Fact]
        public async Task GetHistoricalRates_ShouldReturnBadRequest_WhenCurrenciesAreEmpty()
        {
            var controller = CreateController(
                new FakeExchangeRateHostClient(),
                new FakeExchangeRateResponseMapper());

            var result = await controller.GetHistoricalRates(
                new DateOnly(2025, 1, 1),
                "",
                default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task GetHistoricalRates_ShouldReturn502_WhenMapperThrowsInvalidOperationException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                HistoricalResponse = new HistoricalRatesResponse()
            };

            var mapper = new FakeExchangeRateResponseMapper
            {
                HistoricalException = new InvalidOperationException("bad historical data")
            };

            var controller = CreateController(hostClient, mapper);

            var result = await controller.GetHistoricalRates(
                new DateOnly(2025, 1, 1),
                "CZK",
                default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetHistoricalRates_ShouldReturn502_WhenHostClientThrowsHttpRequestException()
        {
            var hostClient = new FakeExchangeRateHostClient
            {
                HistoricalException = new HttpRequestException("boom")
            };

            var controller = CreateController(hostClient, new FakeExchangeRateResponseMapper());

            var result = await controller.GetHistoricalRates(
                new DateOnly(2025, 1, 1),
                "CZK",
                default);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, objectResult.StatusCode);
        }

        private static RatesController CreateController(
            FakeExchangeRateHostClient hostClient,
            FakeExchangeRateResponseMapper mapper)
        {
            return new RatesController(
                hostClient,
                mapper,
                NullLogger<RatesController>.Instance);
        }

        private sealed class FakeExchangeRateHostClient : IExchangeRateHostClient
        {
            public int LiveCallCount { get; private set; }
            public int HistoricalCallCount { get; private set; }

            public LiveRatesResponse LiveResponse { get; set; } = new();
            public HistoricalRatesResponse HistoricalResponse { get; set; } = new();

            public Exception? LiveException { get; set; }
            public Exception? HistoricalException { get; set; }

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
    }
}