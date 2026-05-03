using CurrencyApp.Core.Models;

namespace CurrencyApp.Api.Services
{
    public interface IExchangeRateCacheService
    {
        Task<ExchangeRateSnapshot?> TryGetLiveSnapshotAsync(
            string baseCurrency,
            CancellationToken cancellationToken = default);

        Task SaveLiveSnapshotAsync(
            ExchangeRateSnapshot snapshot,
            CancellationToken cancellationToken = default);

        Task<ExchangeRateSnapshot?> TryGetHistoricalSnapshotAsync(
            string baseCurrency,
            DateOnly date,
            CancellationToken cancellationToken = default);

        Task SaveHistoricalSnapshotAsync(
            ExchangeRateSnapshot snapshot,
            CancellationToken cancellationToken = default);
    }
}