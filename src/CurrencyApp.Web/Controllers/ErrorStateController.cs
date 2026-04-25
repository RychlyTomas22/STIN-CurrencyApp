using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    public class ErrorStateController : Controller
    {
        public IActionResult ApiFail()
        {
            return View();
        }
    }
}