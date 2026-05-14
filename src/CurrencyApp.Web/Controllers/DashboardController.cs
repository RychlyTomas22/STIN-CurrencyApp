using CurrencyApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private static readonly HashSet<string> SupportedCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "USD",
            "EUR",
            "CZK",
            "GBP",
            "JPY",
            "PLN",
            "CHF"
        };

        private readonly IBackendApiClient _backendApiClient;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IBackendApiClient backendApiClient,
            ILogger<DashboardController> logger)
        {
            _backendApiClient = backendApiClient;
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation(
                "Dashboard page opened. User: {UserName}",
                User.Identity?.Name ?? "unknown");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Analyze(
            [FromBody] DashboardAnalyzeRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because request body was missing. User: {UserName}",
                    User.Identity?.Name ?? "unknown");

                return BadRequest(new
                {
                    error = "Request body is missing."
                });
            }

            var baseCurrency = NormalizeCurrency(request.BaseCurrency);

            var selectedCurrencies = request.Currencies
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(currency => currency.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogInformation(
                "Dashboard analyze requested. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}, StartDate: {StartDate}, EndDate: {EndDate}",
                User.Identity?.Name ?? "unknown",
                baseCurrency,
                string.Join(", ", selectedCurrencies),
                request.StartDate,
                request.EndDate);

            if (string.IsNullOrWhiteSpace(baseCurrency))
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because base currency was empty. User: {UserName}",
                    User.Identity?.Name ?? "unknown");

                return BadRequest(new
                {
                    error = "Base currency is required."
                });
            }

            if (selectedCurrencies.Count == 0)
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because no target currencies were provided. User: {UserName}, BaseCurrency: {BaseCurrency}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency);

                return BadRequest(new
                {
                    error = "At least one target currency is required."
                });
            }

            if (request.StartDate == default || request.EndDate == default)
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because date range was invalid. User: {UserName}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    request.StartDate,
                    request.EndDate);

                return BadRequest(new
                {
                    error = "Start date and end date are required."
                });
            }

            if (request.StartDate > request.EndDate)
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because start date was after end date. User: {UserName}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    request.StartDate,
                    request.EndDate);

                return BadRequest(new
                {
                    error = "Start date cannot be after end date."
                });
            }

            var invalidCurrencies = selectedCurrencies
                .Append(baseCurrency)
                .Where(currency => !SupportedCurrencyCodes.Contains(currency))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidCurrencies.Count > 0)
            {
                _logger.LogWarning(
                    "Dashboard analyze request rejected because unsupported currencies were provided. User: {UserName}, InvalidCurrencies: {InvalidCurrencies}",
                    User.Identity?.Name ?? "unknown",
                    string.Join(", ", invalidCurrencies));

                return BadRequest(new
                {
                    error = "Unsupported currency code.",
                    invalidCurrencies
                });
            }

            try
            {
                var result = await _backendApiClient.AnalyzeAsync(
                    baseCurrency,
                    string.Join(",", selectedCurrencies),
                    request.StartDate,
                    request.EndDate,
                    cancellationToken);

                _logger.LogInformation(
                    "Dashboard analyze completed successfully. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency,
                    string.Join(", ", selectedCurrencies),
                    request.StartDate,
                    request.EndDate);

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "Dashboard analyze failed because backend API request failed. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency,
                    string.Join(", ", selectedCurrencies),
                    request.StartDate,
                    request.EndDate);

                return StatusCode(502, new
                {
                    error = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(
                    ex,
                    "Dashboard analyze failed because backend API returned invalid operation. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency,
                    string.Join(", ", selectedCurrencies),
                    request.StartDate,
                    request.EndDate);

                return StatusCode(502, new
                {
                    error = ex.Message
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Dashboard analyze request was cancelled. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency,
                    string.Join(", ", selectedCurrencies));

                return StatusCode(499, new
                {
                    error = "Request was cancelled."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Dashboard analyze failed unexpectedly. User: {UserName}, BaseCurrency: {BaseCurrency}, Currencies: {Currencies}, StartDate: {StartDate}, EndDate: {EndDate}",
                    User.Identity?.Name ?? "unknown",
                    baseCurrency,
                    string.Join(", ", selectedCurrencies),
                    request.StartDate,
                    request.EndDate);

                return StatusCode(500, new
                {
                    error = "Unexpected dashboard error."
                });
            }
        }

        private static string NormalizeCurrency(string? currency)
        {
            return string.IsNullOrWhiteSpace(currency)
                ? string.Empty
                : currency.Trim().ToUpperInvariant();
        }
    }

    public class DashboardAnalyzeRequest
    {
        public string BaseCurrency { get; set; } = string.Empty;
        public string Currencies { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}