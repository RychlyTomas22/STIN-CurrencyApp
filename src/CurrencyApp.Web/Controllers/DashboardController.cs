using CurrencyApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IBackendApiClient _backendApiClient;

        public DashboardController(IBackendApiClient backendApiClient)
        {
            _backendApiClient = backendApiClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Analyze(
            [FromBody] DashboardAnalyzeRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _backendApiClient.AnalyzeAsync(
                    request.Currencies,
                    request.StartDate,
                    request.EndDate,
                    cancellationToken);

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
        }
    }

    public class DashboardAnalyzeRequest
    {
        public string Currencies { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }
}