using CurrencyApp.Api.Controllers;
using CurrencyApp.Api.Services;
using CurrencyApp.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CurrencyApp.Tests
{
    public class SettingsControllerTests
    {
        [Fact]
        public async Task Get_ShouldReturnOk_WithSettingsFromService()
        {
            var service = new FakeUserSettingsService
            {
                CurrentSettings = new UserSettings
                {
                    BaseCurrency = "USD",
                    SelectedCurrencies = new List<string> { "CZK", "EUR" }
                }
            };

            var controller = CreateController(service);

            var result = await controller.Get(default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<UserSettings>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Equal(2, payload.SelectedCurrencies.Count);
            Assert.Contains("CZK", payload.SelectedCurrencies);
            Assert.Contains("EUR", payload.SelectedCurrencies);
        }

        [Fact]
        public async Task Save_ShouldReturnBadRequest_WhenSettingsIsNull()
        {
            var controller = CreateController(new FakeUserSettingsService());

            var result = await controller.Save(null!, default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Save_ShouldReturnBadRequest_WhenBaseCurrencyIsMissing()
        {
            var controller = CreateController(new FakeUserSettingsService());

            var result = await controller.Save(
                new UserSettings
                {
                    BaseCurrency = "   ",
                    SelectedCurrencies = new List<string> { "CZK" }
                },
                default);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task Save_ShouldNormalizeAndPersistSettings_WhenPayloadIsValid()
        {
            var service = new FakeUserSettingsService();
            var controller = CreateController(service);

            var input = new UserSettings
            {
                BaseCurrency = " eur ",
                SelectedCurrencies = new List<string>
                {
                    " czk ",
                    "USD",
                    "czk",
                    "",
                    "   "
                }
            };

            var result = await controller.Save(input, default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<UserSettings>(ok.Value);

            Assert.Equal("EUR", payload.BaseCurrency);
            Assert.Equal(2, payload.SelectedCurrencies.Count);
            Assert.Equal("CZK", payload.SelectedCurrencies[0]);
            Assert.Equal("USD", payload.SelectedCurrencies[1]);

            Assert.NotNull(service.LastSavedSettings);
            Assert.Equal("EUR", service.LastSavedSettings!.BaseCurrency);
            Assert.Equal(2, service.LastSavedSettings.SelectedCurrencies.Count);
            Assert.Equal("CZK", service.LastSavedSettings.SelectedCurrencies[0]);
            Assert.Equal("USD", service.LastSavedSettings.SelectedCurrencies[1]);
        }

        private static SettingsController CreateController(FakeUserSettingsService service)
        {
            return new SettingsController(
                service,
                NullLogger<SettingsController>.Instance);
        }

        private sealed class FakeUserSettingsService : IUserSettingsService
        {
            public UserSettings CurrentSettings { get; set; } = new()
            {
                BaseCurrency = "USD",
                SelectedCurrencies = new List<string> { "CZK" }
            };

            public UserSettings? LastSavedSettings { get; private set; }

            public Task<UserSettings> GetAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CurrentSettings);
            }

            public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
            {
                LastSavedSettings = new UserSettings
                {
                    BaseCurrency = settings.BaseCurrency,
                    SelectedCurrencies = settings.SelectedCurrencies.ToList()
                };

                CurrentSettings = LastSavedSettings;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Save_ShouldHandleNullSelectedCurrencies_WhenPayloadIsValid()
        {
            var service = new FakeUserSettingsService();
            var controller = CreateController(service);

            var input = new UserSettings
            {
                BaseCurrency = " usd ",
                SelectedCurrencies = null!
            };

            var result = await controller.Save(input, default);

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<UserSettings>(ok.Value);

            Assert.Equal("USD", payload.BaseCurrency);
            Assert.Empty(payload.SelectedCurrencies);

            Assert.NotNull(service.LastSavedSettings);
            Assert.Equal("USD", service.LastSavedSettings!.BaseCurrency);
            Assert.Empty(service.LastSavedSettings.SelectedCurrencies);
        }
    }
}