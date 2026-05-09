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
            [FromQuery] string currencies,
            [FromQuery] DateOnly startDate,
            [FromQuery] DateOnly endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var parsedCurrencies = ParseCurrencies(currencies);

                var selectedCurrencies = new HashSet<string>(
                    parsedCurrencies,
                    StringComparer.OrdinalIgnoreCase);

                var dates = DateRangeHelper.GetDatesInclusive(startDate, endDate);

                if (dates.Count == 0)
                {
                    return BadRequest(new { error = "Date range must contain at least one day." });
                }

                const string sourceCurrency = "USD";

                var currentSnapshot =
                    await _exchangeRateCacheService.TryGetLiveSnapshotAsync(
                        sourceCurrency,
                        cancellationToken);

                if (currentSnapshot is null)
                {
                    _logger.LogInformation(
                        "Live snapshot not found in cache for base currency {BaseCurrency}. Fetching from ExchangeRateHost.",
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
                        "Cached live snapshot does not contain requested currencies. Fetching fresh live snapshot.");

                    var currentLiveResponse = await _exchangeRateHostClient.GetLiveRatesAsync(
                        parsedCurrencies,
                        sourceCurrency,
                        cancellationToken);

                    currentSnapshot = _responseMapper.MapLive(currentLiveResponse);

                    await _exchangeRateCacheService.SaveLiveSnapshotAsync(
                        currentSnapshot,
                        cancellationToken);

                    filteredCurrentSnapshot = FilterSnapshot(currentSnapshot, selectedCurrencies);

                    if (filteredCurrentSnapshot.Rates.Count == 0)
                    {
                        return BadRequest(new { error = "No requested currencies were found in the current snapshot." });
                    }
                }

                var historicalSnapshots = new List<ExchangeRateSnapshot>();

                foreach (var date in dates)
                {
                    var historicalSnapshot =
                        await _exchangeRateCacheService.TryGetHistoricalSnapshotAsync(
                            sourceCurrency,
                            date,
                            cancellationToken);

                    if (historicalSnapshot is null)
                    {
                        _logger.LogInformation(
                            "Historical snapshot for {Date} and base currency {BaseCurrency} not found in cache. Fetching from ExchangeRateHost.",
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

                    var filteredHistoricalSnapshot = FilterSnapshot(historicalSnapshot, selectedCurrencies);

                    if (filteredHistoricalSnapshot.Rates.Count == 0)
                    {
                        _logger.LogInformation(
                            "Cached historical snapshot for {Date} does not contain requested currencies. Fetching fresh historical snapshot.",
                            date);

                        var historicalResponse = await _exchangeRateHostClient.GetHistoricalRatesAsync(
                            date,
                            parsedCurrencies,
                            sourceCurrency,
                            cancellationToken);

                        historicalSnapshot = _responseMapper.MapHistorical(historicalResponse);

                        await _exchangeRateCacheService.SaveHistoricalSnapshotAsync(
                            historicalSnapshot,
                            cancellationToken);

                        filteredHistoricalSnapshot = FilterSnapshot(historicalSnapshot, selectedCurrencies);
                    }

                    historicalSnapshots.Add(filteredHistoricalSnapshot);
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