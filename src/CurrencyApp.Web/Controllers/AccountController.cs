using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Web.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}