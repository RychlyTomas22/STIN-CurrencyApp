using System.Text.Json;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Core.Models;
using Microsoft.Extensions.Options;

namespace CurrencyApp.Api.Services
{
    public class FileUserSettingsService : IUserSettingsService
    {
        private readonly UserSettingsStorageOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUserSettingsService> _logger;

        public FileUserSettingsService(
            IOptions<UserSettingsStorageOptions> options,
            IWebHostEnvironment environment,
            ILogger<FileUserSettingsService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<UserSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            var path = GetFilePath();

            if (!File.Exists(path))
            {
                return new UserSettings
                {
                    BaseCurrency = "USD",
                    SelectedCurrencies = new List<string> { "CZK" }
                };
            }

            await using var stream = File.OpenRead(path);

            var settings = await JsonSerializer.DeserializeAsync<UserSettings>(
                stream,
                cancellationToken: cancellationToken);

            return settings ?? new UserSettings
            {
                BaseCurrency = "USD",
                SelectedCurrencies = new List<string> { "CZK" }
            };
        }

        public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(path);

            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                },
                cancellationToken);

            _logger.LogInformation("User settings saved to {Path}", path);
        }

        private string GetFilePath()
        {
            var configuredPath = _options.FilePath;

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.Combine(_environment.ContentRootPath, configuredPath);
        }
    }
}