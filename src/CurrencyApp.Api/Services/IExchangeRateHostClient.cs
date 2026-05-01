using CurrencyApp.Api.Models.ExchangeRateHost;

namespace CurrencyApp.Api.Services
{
    public interface IExchangeRateHostClient
    {
        Task<LiveRatesResponse> GetLiveRatesAsync(
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default);

        Task<HistoricalRatesResponse> GetHistoricalRatesAsync(
            DateOnly date,
            IEnumerable<string> currencies,
            string? source = null,
            CancellationToken cancellationToken = default);
    }
}