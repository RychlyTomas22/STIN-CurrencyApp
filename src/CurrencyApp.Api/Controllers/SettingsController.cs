using CurrencyApp.Core.Models;
using CurrencyApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            IUserSettingsService userSettingsService,
            ILogger<SettingsController> logger)
        {
            _userSettingsService = userSettingsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var settings = await _userSettingsService.GetAsync(cancellationToken);
            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> Save(
            [FromBody] UserSettings settings,
            CancellationToken cancellationToken)
        {
            if (settings is null)
            {
                return BadRequest(new { error = "Settings payload is required." });
            }

            if (string.IsNullOrWhiteSpace(settings.BaseCurrency))
            {
                return BadRequest(new { error = "BaseCurrency is required." });
            }

            settings.BaseCurrency = settings.BaseCurrency.Trim().ToUpperInvariant();

            settings.SelectedCurrencies = settings.SelectedCurrencies?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct()
                .ToList()
                ?? new List<string>();

            await _userSettingsService.SaveAsync(settings, cancellationToken);

            _logger.LogInformation("User settings updated.");

            return Ok(settings);
        }
    }
}