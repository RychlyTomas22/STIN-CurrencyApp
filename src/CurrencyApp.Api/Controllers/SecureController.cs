using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecureController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                status = "authenticated",
                user = User.Identity?.Name ?? "unknown",
                application = "CurrencyApp.Api",
                timestampUtc = DateTime.UtcNow
            });
        }
    }
}