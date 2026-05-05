using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CurrencyApp.Web.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IConfiguration _configuration;

        public SettingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewData["BackendApiBaseUrl"] =
                _configuration["BackendApi:BaseUrl"]
                ?? throw new InvalidOperationException("Missing configuration value: BackendApi:BaseUrl");

            return View();
        }
    }
}