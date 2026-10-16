using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace sideral_api.Controllers;

[Route("api/ping")]
[AllowAnonymous]
public class PingController : BaseController
{
    [HttpGet]
    public IActionResult Ping()
    {
        return Ok(new
        {
            status = "ok",
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            time = DateTime.UtcNow
        });
    }
}
