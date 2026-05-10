using System.Net.Http.Json;
using CurrencyApp.Core.Models;

namespace CurrencyApp.Web.Services
{
    public class BackendApiClient : IBackendApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public BackendApiClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<CurrencyAnalysisResult> AnalyzeAsync(
            string baseCurrency,
            string currencies,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            var normalizedBaseCurrency = string.IsNullOrWhiteSpace(baseCurrency)
                ? "USD"
                : baseCurrency.Trim().ToUpperInvariant();

            var url =
                $"api/analysis?baseCurrency={Uri.EscapeDataString(normalizedBaseCurrency)}" +
                $"&currencies={Uri.EscapeDataString(currencies)}" +
                $"&startDate={startDate:yyyy-MM-dd}" +
                $"&endDate={endDate:yyyy-MM-dd}";

            using var response = await client.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Backend API returned HTTP {(int)response.StatusCode}. Response: {responseText}");
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<CurrencyAnalysisResult>(
                responseText,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result ?? throw new InvalidOperationException("Backend returned no analysis result.");
        }

        public async Task<UserSettings> GetSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            using var response = await client.GetAsync("api/settings", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Backend API returned HTTP {(int)response.StatusCode}. Response: {responseText}");
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(
                responseText,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result ?? throw new InvalidOperationException("Backend returned no settings.");
        }

        public async Task<UserSettings> SaveSettingsAsync(
            UserSettings settings,
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            using var response = await client.PutAsJsonAsync("api/settings", settings, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Backend API returned HTTP {(int)response.StatusCode}. Response: {responseText}");
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(
                responseText,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result ?? throw new InvalidOperationException("Backend returned no saved settings.");
        }
    }
}