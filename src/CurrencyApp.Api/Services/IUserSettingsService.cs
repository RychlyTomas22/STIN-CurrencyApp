using CurrencyApp.Core.Models;

namespace CurrencyApp.Api.Services
{
    public interface IUserSettingsService
    {
        Task<UserSettings> GetAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
    }
}