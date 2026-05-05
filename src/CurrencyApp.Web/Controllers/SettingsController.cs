using CurrencyApp.Core.Models;
using CurrencyApp.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IBackendApiClient _backendApiClient;

        public SettingsController(IBackendApiClient backendApiClient)
        {
            _backendApiClient = backendApiClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Load(CancellationToken cancellationToken)
        {
            try
            {
                var settings = await _backendApiClient.GetSettingsAsync(cancellationToken);
                return Ok(settings);
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

        [HttpPost]
        public async Task<IActionResult> Save(
            [FromBody] UserSettings settings,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _backendApiClient.SaveSettingsAsync(settings, cancellationToken);
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
}