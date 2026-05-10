using CurrencyApp.Core.Models;

namespace CurrencyApp.Web.Services
{
    public interface IBackendApiClient
    {
        Task<CurrencyAnalysisResult> AnalyzeAsync(
            string BaseCurrency,
            string currencies,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default);

        Task<UserSettings> GetSettingsAsync(
            CancellationToken cancellationToken = default);

        Task<UserSettings> SaveSettingsAsync(
            UserSettings settings,
            CancellationToken cancellationToken = default);
    }
}