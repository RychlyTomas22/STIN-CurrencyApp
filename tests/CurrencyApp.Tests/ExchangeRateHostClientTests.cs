using System.Net;
using System.Text;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Api.Models.ExchangeRateHost;
using CurrencyApp.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CurrencyApp.Tests
{
    public class ExchangeRateHostClientTests
    {
        [Fact]
        public async Task GetLiveRatesAsync_ShouldSendNormalizedCurrencies_AndReturnResponse()
        {
            var handler = new FakeHttpMessageHandler(_ =>
            {
                var json = """
                {
                  "success": true,
                  "timestamp": 1735689600,
                  "source": "USD",
                  "quotes": {
                    "USDCZK": 24.5,
                    "USDEUR": 0.85
                  }
                }
                """;

                return CreateJsonResponse(HttpStatusCode.OK, json);
            });

            var client = CreateClient(handler);
            var service = CreateService(client, accessKey: "test-key");

            var result = await service.GetLiveRatesAsync(new[] { " czk ", "EUR", "czk", "" });

            Assert.Equal("USD", result.Source);
            Assert.Equal(2, result.Quotes.Count);
            Assert.Equal(24.5m, result.Quotes["USDCZK"]);
            Assert.Equal(0.85m, result.Quotes["USDEUR"]);

            Assert.NotNull(handler.LastRequestUri);

            var requestUri = handler.LastRequestUri!;
            Assert.Equal("/live", requestUri.AbsolutePath);

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(requestUri.Query);

            Assert.Equal("test-key", query["access_key"].ToString());
            Assert.Equal("CZK,EUR", query["currencies"].ToString());
            Assert.False(query.ContainsKey("source"));
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ShouldIncludeDateAndUppercaseSource()
        {
            var handler = new FakeHttpMessageHandler(_ =>
            {
                var json = """
                {
                  "success": true,
                  "historical": true,
                  "date": "2025-01-01",
                  "source": "USD",
                  "quotes": {
                    "USDCZK": 24.3
                  }
                }
                """;

                return CreateJsonResponse(HttpStatusCode.OK, json);
            });

            var client = CreateClient(handler);
            var service = CreateService(client, accessKey: "abc123");

            var result = await service.GetHistoricalRatesAsync(
                new DateOnly(2025, 1, 1),
                new[] { "czk" },
                source: " usd ");

            Assert.Equal("USD", result.Source);
            Assert.Equal("2025-01-01", result.Date);
            Assert.Single(result.Quotes);
            Assert.Equal(24.3m, result.Quotes["USDCZK"]);

            var requestUri = handler.LastRequestUri!;
            Assert.Equal("/historical", requestUri.AbsolutePath);

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(requestUri.Query);

            Assert.Equal("abc123", query["access_key"].ToString());
            Assert.Equal("CZK", query["currencies"].ToString());
            Assert.Equal("2025-01-01", query["date"].ToString());
            Assert.Equal("USD", query["source"].ToString());
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldThrow_WhenCurrenciesAreEmpty()
        {
            var client = CreateClient(new FakeHttpMessageHandler(_ =>
                CreateJsonResponse(HttpStatusCode.OK, "{}")));

            var service = CreateService(client, accessKey: "test-key");

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetLiveRatesAsync(new[] { "", "   " }));

            Assert.Equal("currencies", ex.ParamName);
            Assert.Contains("At least one currency must be provided", ex.Message);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldThrowHttpRequestException_WhenHttpStatusIsNotSuccessful()
        {
            var handler = new FakeHttpMessageHandler(_ =>
                CreateJsonResponse(HttpStatusCode.TooManyRequests, """
                {
                  "error": "rate limited"
                }
                """));

            var client = CreateClient(handler);
            var service = CreateService(client, accessKey: "test-key");

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.GetLiveRatesAsync(new[] { "CZK" }));

            Assert.Contains("HTTP 429", ex.Message);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldThrowInvalidOperationException_WhenApiReturnsSuccessFalse()
        {
            var handler = new FakeHttpMessageHandler(_ =>
                CreateJsonResponse(HttpStatusCode.OK, """
                {
                  "success": false,
                  "error": {
                    "code": 101,
                    "info": "Invalid access key."
                  }
                }
                """));

            var client = CreateClient(handler);
            var service = CreateService(client, accessKey: "test-key");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetLiveRatesAsync(new[] { "CZK" }));

            Assert.Contains("API error 101", ex.Message);
            Assert.Contains("Invalid access key", ex.Message);
        }

        [Fact]
        public async Task GetLiveRatesAsync_ShouldThrowInvalidOperationException_WhenResponseCannotBeDeserialized()
        {
            var handler = new FakeHttpMessageHandler(_ =>
                CreateJsonResponse(HttpStatusCode.OK, "null"));

            var client = CreateClient(handler);
            var service = CreateService(client, accessKey: "test-key");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetLiveRatesAsync(new[] { "CZK" }));

            Assert.Contains("Failed to deserialize ExchangeRateHost response", ex.Message);
        }

        private static ExchangeRateHostClient CreateService(HttpClient client, string accessKey)
        {
            var factory = new FakeHttpClientFactory(client);

            var options = Options.Create(new ExchangeRateHostOptions
            {
                BaseUrl = "https://api.exchangerate.host/",
                TimeoutSeconds = 10,
                AccessKey = accessKey
            });

            return new ExchangeRateHostClient(
                factory,
                options,
                NullLogger<ExchangeRateHostClient>.Instance);
        }

        private static HttpClient CreateClient(HttpMessageHandler handler)
        {
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.exchangerate.host/")
            };
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public FakeHttpClientFactory(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name)
            {
                return _client;
            }
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public Uri? LastRequestUri { get; private set; }

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                return Task.FromResult(_handler(request));
            }
        }
    }
}