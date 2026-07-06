// Copy to backend/FinancialServices.Api/Controllers/PortfoliosController.cs
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/portfolios")]
public sealed class PortfoliosController(IPortfolioService service) : ControllerBase
{
    [HttpGet("{portfolioId}")]
    [ProducesResponseType<PortfolioSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioSummary>> GetPortfolio(string portfolioId, CancellationToken ct)
    {
        var portfolio = await service.GetPortfolioAsync(portfolioId, ct);
        return portfolio is null
            ? Problem(statusCode: StatusCodes.Status404NotFound,
                title: "PORTFOLIO_NOT_FOUND",
                detail: $"Portfolio {portfolioId} not found")
            : Ok(portfolio);
    }

    [HttpPost("{portfolioId}/rebalance")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Rebalance(
        string portfolioId, [FromBody] RebalanceRequest request, CancellationToken ct)
    {
        var jobId = await service.SubmitRebalanceAsync(portfolioId, request, ct);
        return Accepted(new { jobId, status = "accepted" });
    }
}
