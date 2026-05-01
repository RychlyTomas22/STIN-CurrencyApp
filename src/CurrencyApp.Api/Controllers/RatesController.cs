using CurrencyApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RatesController : ControllerBase
    {
        private readonly IExchangeRateHostClient _exchangeRateHostClient;
        private readonly ILogger<RatesController> _logger;

        public RatesController(
            IExchangeRateHostClient exchangeRateHostClient,
            ILogger<RatesController> logger)
        {
            _exchangeRateHostClient = exchangeRateHostClient;
            _logger = logger;
        }

        [HttpGet("live")]
        public async Task<IActionResult> GetLiveRates(
            [FromQuery] string currencies,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsedCurrencies = ParseCurrencies(currencies);

                var response = await _exchangeRateHostClient.GetLiveRatesAsync(
                    parsedCurrencies,
                    cancellationToken: cancellationToken);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid live rates request.");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "ExchangeRateHost returned an API error for live rates.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while calling ExchangeRateHost for live rates.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
        }

        [HttpGet("historical")]
        public async Task<IActionResult> GetHistoricalRates(
            [FromQuery] DateOnly date,
            [FromQuery] string currencies,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsedCurrencies = ParseCurrencies(currencies);

                var response = await _exchangeRateHostClient.GetHistoricalRatesAsync(
                    date,
                    parsedCurrencies,
                    cancellationToken: cancellationToken);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid historical rates request.");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "ExchangeRateHost returned an API error for historical rates.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while calling ExchangeRateHost for historical rates.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
        }

        private static List<string> ParseCurrencies(string currencies)
        {
            var parsedCurrencies = currencies
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.ToUpperInvariant())
                .Distinct()
                .ToList();

            if (parsedCurrencies.Count == 0)
            {
                throw new ArgumentException("At least one currency must be provided.");
            }

            return parsedCurrencies;
        }
    }
}