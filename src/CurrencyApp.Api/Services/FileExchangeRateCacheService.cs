using System.Text.Json;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Core.Models;
using Microsoft.Extensions.Options;

namespace CurrencyApp.Api.Services
{
    public class FileExchangeRateCacheService : IExchangeRateCacheService
    {
        private readonly ExchangeRateCacheOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileExchangeRateCacheService> _logger;

        public FileExchangeRateCacheService(
            IOptions<ExchangeRateCacheOptions> options,
            IWebHostEnvironment environment,
            ILogger<FileExchangeRateCacheService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ExchangeRateSnapshot?> TryGetLiveSnapshotAsync(
            string baseCurrency,
            CancellationToken cancellationToken = default)
        {
            var path = GetLivePath(baseCurrency);

            if (!File.Exists(path))
            {
                return null;
            }

            return await ReadSnapshotAsync(path, cancellationToken);
        }

        public async Task SaveLiveSnapshotAsync(
            ExchangeRateSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var path = GetLivePath(snapshot.BaseCurrency);
            await WriteSnapshotAsync(path, snapshot, cancellationToken);
        }

        public async Task<ExchangeRateSnapshot?> TryGetHistoricalSnapshotAsync(
            string baseCurrency,
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            var path = GetHistoricalPath(baseCurrency, date);

            if (!File.Exists(path))
            {
                return null;
            }

            return await ReadSnapshotAsync(path, cancellationToken);
        }

        public async Task SaveHistoricalSnapshotAsync(
            ExchangeRateSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var path = GetHistoricalPath(snapshot.BaseCurrency, snapshot.Date);
            await WriteSnapshotAsync(path, snapshot, cancellationToken);
        }

        private async Task<ExchangeRateSnapshot?> ReadSnapshotAsync(
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(path);

            var snapshot = await JsonSerializer.DeserializeAsync<ExchangeRateSnapshot>(
                stream,
                cancellationToken: cancellationToken);

            return snapshot;
        }

        private async Task WriteSnapshotAsync(
            string path,
            ExchangeRateSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(path);

            await JsonSerializer.SerializeAsync(
                stream,
                snapshot,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                },
                cancellationToken);

            _logger.LogInformation("Saved exchange rate snapshot to cache: {Path}", path);
        }

        private string GetLivePath(string baseCurrency)
        {
            var root = GetRootPath();
            return Path.Combine(root, NormalizeCurrency(baseCurrency), "live.json");
        }

        private string GetHistoricalPath(string baseCurrency, DateOnly date)
        {
            var root = GetRootPath();
            return Path.Combine(
                root,
                NormalizeCurrency(baseCurrency),
                "historical",
                $"{date:yyyy-MM-dd}.json");
        }

        private string GetRootPath()
        {
            var configuredPath = _options.RootPath;

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.Combine(_environment.ContentRootPath, configuredPath);
        }

        private static string NormalizeCurrency(string currency)
        {
            return currency.Trim().ToUpperInvariant();
        }
    }
}