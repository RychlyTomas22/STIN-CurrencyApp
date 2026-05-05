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
    public class FileUserSettingsServiceTests
    {
        [Fact]
        public async Task GetAsync_ShouldReturnDefaultSettings_WhenFileDoesNotExist()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredFilePath: "data/user-settings/user-settings.json");

                var result = await service.GetAsync();

                Assert.Equal("USD", result.BaseCurrency);
                Assert.Single(result.SelectedCurrencies);
                Assert.Equal("CZK", result.SelectedCurrencies[0]);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task SaveAsync_AndGetAsync_ShouldPersistSettings_UsingRelativePath()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredFilePath: "data/user-settings/user-settings.json");

                var settings = new UserSettings
                {
                    BaseCurrency = "EUR",
                    SelectedCurrencies = new List<string> { "CZK", "USD" }
                };

                await service.SaveAsync(settings);

                var expectedPath = Path.Combine(
                    tempRoot,
                    "data",
                    "user-settings",
                    "user-settings.json");

                Assert.True(File.Exists(expectedPath));

                var loaded = await service.GetAsync();

                Assert.Equal("EUR", loaded.BaseCurrency);
                Assert.Equal(2, loaded.SelectedCurrencies.Count);
                Assert.Contains("CZK", loaded.SelectedCurrencies);
                Assert.Contains("USD", loaded.SelectedCurrencies);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task GetAsync_ShouldReturnDefaultSettings_WhenStoredJsonIsNull()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var relativePath = "data/user-settings/user-settings.json";
                var absolutePath = Path.Combine(tempRoot, "data", "user-settings", "user-settings.json");

                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                await File.WriteAllTextAsync(absolutePath, "null", Encoding.UTF8);

                var service = CreateService(
                    contentRootPath: tempRoot,
                    configuredFilePath: relativePath);

                var result = await service.GetAsync();

                Assert.Equal("USD", result.BaseCurrency);
                Assert.Single(result.SelectedCurrencies);
                Assert.Equal("CZK", result.SelectedCurrencies[0]);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task SaveAsync_ShouldUseAbsolutePath_WhenConfiguredPathIsRooted()
        {
            var tempRoot = CreateTempDirectory();

            try
            {
                var absoluteFilePath = Path.Combine(tempRoot, "absolute-settings.json");

                var service = CreateService(
                    contentRootPath: Path.Combine(tempRoot, "some-other-root"),
                    configuredFilePath: absoluteFilePath);

                var settings = new UserSettings
                {
                    BaseCurrency = "GBP",
                    SelectedCurrencies = new List<string> { "EUR" }
                };

                await service.SaveAsync(settings);

                Assert.True(File.Exists(absoluteFilePath));

                var loaded = await service.GetAsync();

                Assert.Equal("GBP", loaded.BaseCurrency);
                Assert.Single(loaded.SelectedCurrencies);
                Assert.Equal("EUR", loaded.SelectedCurrencies[0]);
            }
            finally
            {
                DeleteDirectory(tempRoot);
            }
        }

        private static FileUserSettingsService CreateService(
            string contentRootPath,
            string configuredFilePath)
        {
            var options = Options.Create(new UserSettingsStorageOptions
            {
                FilePath = configuredFilePath
            });

            var environment = new FakeWebHostEnvironment
            {
                ContentRootPath = contentRootPath
            };

            return new FileUserSettingsService(
                options,
                environment,
                NullLogger<FileUserSettingsService>.Instance);
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