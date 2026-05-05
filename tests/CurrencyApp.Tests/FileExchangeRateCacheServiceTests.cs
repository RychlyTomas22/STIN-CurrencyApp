using System.Text;
using CurrencyApp.Api.Configuration;
using CurrencyApp.Api.Services;
using CurrencyApp.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CurrencyApp.Tests
{
    public class FileExchangeRateCacheServiceTests
    {
        [Fact]
        public async Task TryGetLiveSnapshotAsync_ShouldReturnNull_WhenFileDoesNotExist()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredRootPath: "data/exchange-rate-cache");

                var result = await service.TryGetLiveSnapshotAsync("usd");

                Assert.Null(result);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task SaveLiveSnapshotAsync_AndTryGetLiveSnapshotAsync_ShouldPersistSnapshot_UsingRelativePath()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredRootPath: "data/exchange-rate-cache");

                var snapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "usd",
                    Date = new DateOnly(2025, 1, 1),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.5m },
                        new() { Currency = "EUR", Rate = 0.85m }
                    }
                };

                await service.SaveLiveSnapshotAsync(snapshot);

                var expectedPath = Path.Combine(
                    tempRoot,
                    "data",
                    "exchange-rate-cache",
                    "USD",
                    "live.json");

                Assert.True(File.Exists(expectedPath));

                var loaded = await service.TryGetLiveSnapshotAsync("USD");

                Assert.NotNull(loaded);
                Assert.Equal("usd", loaded!.BaseCurrency);
                Assert.Equal(new DateOnly(2025, 1, 1), loaded.Date);
                Assert.Equal(2, loaded.Rates.Count);
                Assert.Contains(loaded.Rates, x => x.Currency == "CZK" && x.Rate == 24.5m);
                Assert.Contains(loaded.Rates, x => x.Currency == "EUR" && x.Rate == 0.85m);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task TryGetHistoricalSnapshotAsync_ShouldReturnNull_WhenFileDoesNotExist()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredRootPath: "data/exchange-rate-cache");

                var result = await service.TryGetHistoricalSnapshotAsync(
                    "USD",
                    new DateOnly(2025, 1, 1));

                Assert.Null(result);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task SaveHistoricalSnapshotAsync_AndTryGetHistoricalSnapshotAsync_ShouldPersistSnapshot_UsingAbsolutePath()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var absoluteRootPath = Path.Combine(tempRoot, "absolute-cache-root");

                var service = CreateService(
                    contentRootPath: Path.Combine(tempRoot, "some-other-root"),
                    configuredRootPath: absoluteRootPath);

                var snapshot = new ExchangeRateSnapshot
                {
                    BaseCurrency = "usd",
                    Date = new DateOnly(2025, 1, 2),
                    Rates = new List<ExchangeRateValue>
                    {
                        new() { Currency = "CZK", Rate = 24.3m }
                    }
                };

                await service.SaveHistoricalSnapshotAsync(snapshot);

                var expectedPath = Path.Combine(
                    absoluteRootPath,
                    "USD",
                    "historical",
                    "2025-01-02.json");

                Assert.True(File.Exists(expectedPath));

                var loaded = await service.TryGetHistoricalSnapshotAsync(
                    "USD",
                    new DateOnly(2025, 1, 2));

                Assert.NotNull(loaded);
                Assert.Equal("usd", loaded!.BaseCurrency);
                Assert.Equal(new DateOnly(2025, 1, 2), loaded.Date);
                Assert.Single(loaded.Rates);
                Assert.Equal("CZK", loaded.Rates[0].Currency);
                Assert.Equal(24.3m, loaded.Rates[0].Rate);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task TryGetLiveSnapshotAsync_ShouldReturnNull_WhenStoredJsonIsNull()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var liveFilePath = Path.Combine(
                    tempRoot,
                    "data",
                    "exchange-rate-cache",
                    "USD",
                    "live.json");

                Directory.CreateDirectory(Path.GetDirectoryName(liveFilePath)!);
                await File.WriteAllTextAsync(liveFilePath, "null", Encoding.UTF8);

                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredRootPath: "data/exchange-rate-cache");

                var result = await service.TryGetLiveSnapshotAsync("USD");

                Assert.Null(result);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        private static FileExchangeRateCacheService CreateService(
            string contentRootPath,
            string configuredRootPath)
        {
            var options = Options.Create(new ExchangeRateCacheOptions
            {
                RootPath = configuredRootPath
            });

            var environment = new FakeWebHostEnvironment
            {
                ContentRootPath = contentRootPath
            };

            return new FileExchangeRateCacheService(
                options,
                environment,
                NullLogger<FileExchangeRateCacheService>.Instance);
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "CurrencyAppTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private sealed class FakeWebHostEnvironment : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "CurrencyApp.Api";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Development";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}