using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}