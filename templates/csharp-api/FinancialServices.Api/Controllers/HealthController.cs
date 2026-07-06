// Copy to backend/FinancialServices.Api/Controllers/HealthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok", service = "FinancialServices.Api" });
}
