using System.Net.Http.Json;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Api.Models.ExchangeRateHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CurrencyApp.Api.Services
{
    public class ExchangeRateHostClient : IExchangeRateHostClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ExchangeRateHostOptions _options;
        private readonly ILogger<ExchangeRateHostClient> _logger;

        public ExchangeRateHostClient(
            IHttpClientFactory httpClientFactory,
            IOptions<ExchangeRateHostOptions> options,
            ILogger<ExchangeRateHostClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<LiveRatesResponse> GetLiveRatesAsync(
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = CreateBaseQuery(currencies, source);
            var response = await SendRequestAsync<LiveRatesResponse>(
                "live",
                queryParams,
                cancellationToken);

            return response;
        }

        public async Task<HistoricalRatesResponse> GetHistoricalRatesAsync(
            DateOnly date,
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = CreateBaseQuery(currencies, source);
            queryParams["date"] = date.ToString("yyyy-MM-dd");

            var response = await SendRequestAsync<HistoricalRatesResponse>(
                "historical",
                queryParams,
                cancellationToken);

            return response;
        }

        private Dictionary<string, string?> CreateBaseQuery(
            IEnumerable<string> currencies,
            string? source)
        {
            var selectedCurrencies = currencies
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (selectedCurrencies.Count == 0)
            {
                throw new ArgumentException("At least one currency must be provided.", nameof(currencies));
            }

            var queryParams = new Dictionary<string, string?>
            {
                ["access_key"] = _options.AccessKey,
                ["currencies"] = string.Join(",", selectedCurrencies)
            };

            if (!string.IsNullOrWhiteSpace(source))
            {
                queryParams["source"] = source.Trim().ToUpperInvariant();
            }

            return queryParams;
        }

        private async Task<TResponse> SendRequestAsync<TResponse>(
            string endpoint,
            Dictionary<string, string?> queryParams,
            CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient("ExchangeRateHost");
            var requestUrl = QueryHelpers.AddQueryString(endpoint, queryParams);

            _logger.LogInformation("Calling ExchangeRateHost endpoint {Endpoint}", requestUrl);

            using var response = await client.GetAsync(requestUrl, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ExchangeRateHost returned HTTP {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    responseContent);

                throw new HttpRequestException(
                    $"ExchangeRateHost returned HTTP {(int)response.StatusCode}.");
            }

            var errorResponse = System.Text.Json.JsonSerializer.Deserialize<ExchangeRateErrorResponse>(responseContent);

            if (errorResponse is not null && errorResponse.Success == false)
            {
                var errorCode = errorResponse.Error?.Code;
                var errorInfo = errorResponse.Error?.Info ?? "Unknown API error.";

                _logger.LogError(
                    "ExchangeRateHost returned API error {ErrorCode}: {ErrorInfo}",
                    errorCode,
                    errorInfo);

                throw new InvalidOperationException(
                    $"ExchangeRateHost API error {errorCode}: {errorInfo}");
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<TResponse>(
                responseContent,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (result is null)
            {
                _logger.LogError("Failed to deserialize ExchangeRateHost response. Body: {Body}", responseContent);
                throw new InvalidOperationException("Failed to deserialize ExchangeRateHost response.");
            }

            return result;
        }
    }
}