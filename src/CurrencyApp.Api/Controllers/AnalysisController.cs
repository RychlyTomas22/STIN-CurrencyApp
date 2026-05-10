using CurrencyApp.Api.Helpers;
using CurrencyApp.Api.Mappings;
using CurrencyApp.Api.Services;
using CurrencyApp.Core.Models;
using CurrencyApp.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IExchangeRateHostClient _exchangeRateHostClient;
        private readonly IExchangeRateResponseMapper _responseMapper;
        private readonly ICurrencyAnalysisService _currencyAnalysisService;
        private readonly IExchangeRateCacheService _exchangeRateCacheService;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(
            IExchangeRateHostClient exchangeRateHostClient,
            IExchangeRateResponseMapper responseMapper,
            ICurrencyAnalysisService currencyAnalysisService,
            IExchangeRateCacheService exchangeRateCacheService,
            ILogger<AnalysisController> logger)
        {
            _exchangeRateHostClient = exchangeRateHostClient;
            _responseMapper = responseMapper;
            _currencyAnalysisService = currencyAnalysisService;
            _exchangeRateCacheService = exchangeRateCacheService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Analyze(
            [FromQuery] string? baseCurrency,
            [FromQuery] string currencies,
            [FromQuery] DateOnly startDate,
            [FromQuery] DateOnly endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsedCurrencies = ParseCurrencies(currencies);

                var dates = DateRangeHelper.GetDatesInclusive(startDate, endDate);

                var sourceCurrency = string.IsNullOrWhiteSpace(baseCurrency)
                    ? "USD"
                    : baseCurrency.Trim().ToUpperInvariant();

                parsedCurrencies = parsedCurrencies
                    .Where(currency => !string.Equals(
                        currency,
                        sourceCurrency,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (parsedCurrencies.Count == 0)
                {
                    return BadRequest(new
                    {
                        error = "At least one selected currency different from base currency must be provided."
                    });
                }

                var selectedCurrencies = new HashSet<string>(
                    parsedCurrencies,
                    StringComparer.OrdinalIgnoreCase);

                var currentSnapshot =
                    await _exchangeRateCacheService.TryGetLiveSnapshotAsync(
                        sourceCurrency,
                        cancellationToken);

                if (currentSnapshot is null ||
                    !ContainsAllRequestedCurrencies(currentSnapshot, selectedCurrencies))
                {
                    _logger.LogInformation(
                        "Live snapshot for base currency {BaseCurrency} is missing requested currencies. Fetching from ExchangeRateHost.",
                        sourceCurrency);

                    var currentLiveResponse = await _exchangeRateHostClient.GetLiveRatesAsync(
                        parsedCurrencies,
                        sourceCurrency,
                        cancellationToken);

                    currentSnapshot = _responseMapper.MapLive(currentLiveResponse);

                    await _exchangeRateCacheService.SaveLiveSnapshotAsync(
                        currentSnapshot,
                        cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "Using cached live snapshot for base currency {BaseCurrency}.",
                        sourceCurrency);
                }

                var filteredCurrentSnapshot = FilterSnapshot(currentSnapshot, selectedCurrencies);

                if (filteredCurrentSnapshot.Rates.Count == 0)
                {
                    _logger.LogInformation(
                        "Current snapshot does not contain requested currencies after refresh.");

                    return BadRequest(new { error = "No requested currencies were found in the current snapshot." });
                }

                var historicalSnapshots = new List<ExchangeRateSnapshot>();

                foreach (var date in dates)
                {
                    try
                    {
                        var historicalSnapshot =
                            await _exchangeRateCacheService.TryGetHistoricalSnapshotAsync(
                                sourceCurrency,
                                date,
                                cancellationToken);

                        if (historicalSnapshot is null ||
                            !ContainsAllRequestedCurrencies(historicalSnapshot, selectedCurrencies))
                        {
                            _logger.LogInformation(
                                "Historical snapshot for {Date} and base currency {BaseCurrency} is missing requested currencies. Fetching from ExchangeRateHost.",
                                date,
                                sourceCurrency);

                            var historicalResponse = await _exchangeRateHostClient.GetHistoricalRatesAsync(
                                date,
                                parsedCurrencies,
                                sourceCurrency,
                                cancellationToken);

                            historicalSnapshot = _responseMapper.MapHistorical(historicalResponse);

                            await _exchangeRateCacheService.SaveHistoricalSnapshotAsync(
                                historicalSnapshot,
                                cancellationToken);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Using cached historical snapshot for {Date} and base currency {BaseCurrency}.",
                                date,
                                sourceCurrency);
                        }

                        var filteredHistoricalSnapshot = FilterSnapshot(
                            historicalSnapshot,
                            selectedCurrencies);

                        if (filteredHistoricalSnapshot.Rates.Count > 0)
                        {
                            historicalSnapshots.Add(filteredHistoricalSnapshot);
                        }
                    }
                    catch (HttpRequestException ex) when (
                        ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            ex,
                            "Historical data for {Date} could not be loaded because the external API rate limit was reached. This date will be ignored.",
                            date);

                        continue;
                    }
                }

                var analysisResult = _currencyAnalysisService.Analyze(
                    filteredCurrentSnapshot,
                    historicalSnapshots);

                return Ok(analysisResult);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid analysis request.");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Analysis failed due to invalid external data.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while calling ExchangeRateHost for analysis.");
                return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
            }
        }

        private static ExchangeRateSnapshot FilterSnapshot(
            ExchangeRateSnapshot snapshot,
            HashSet<string> selectedCurrencies)
        {
            return new ExchangeRateSnapshot
            {
                BaseCurrency = snapshot.BaseCurrency,
                Date = snapshot.Date,
                Rates = snapshot.Rates
                    .Where(rate => selectedCurrencies.Contains(rate.Currency))
                    .Select(rate => new ExchangeRateValue
                    {
                        Currency = rate.Currency,
                        Rate = rate.Rate
                    })
                    .ToList()
            };
        }

        private static bool ContainsAllRequestedCurrencies(
            ExchangeRateSnapshot snapshot,
            HashSet<string> selectedCurrencies)
        {
            var availableCurrencies = snapshot.Rates
                .Select(rate => rate.Currency)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return selectedCurrencies.All(availableCurrencies.Contains);
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