using System.ComponentModel.DataAnnotations;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services.MarketContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

/// <summary>
/// Live third-party <b>market-context</b> enrichment (separate from Prism's own rating reconciliation).
/// Currently one source — Morningstar analyst research for a real listed security. Thin: delegates to
/// <see cref="IMorningstarContextService"/>, which always returns a renderable payload (provider off /
/// not covered / re-login / unavailable are conveyed in the body, not as a 5xx — P1 fail-soft).
/// <b>Path to customer build:</b> same as the rest of the API — add Entra auth before shipping.
/// </summary>
[ApiController]
[Route("api/v1/market-context")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class MarketContextController(IMorningstarContextService morningstar) : ControllerBase
{
    /// <summary>
    /// Fetches live Morningstar analyst research for <paramref name="identifier"/> (a real ticker, ISIN,
    /// or company name). Fictional/illustrative issuers return <c>NotCovered</c> — Morningstar only knows
    /// listed securities.
    /// </summary>
    [HttpGet("morningstar")]
    [ProducesResponseType<MorningstarContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorBody>(StatusCodes.Status400BadRequest)]
    public async Task<MorningstarContextResponse> GetMorningstar(
        [FromQuery]
        [Required]
        [StringLength(64, MinimumLength = 1)]
        [RegularExpression(@"^[A-Za-z0-9 .&\-]+$", ErrorMessage = "identifier may contain only letters, digits, spaces, and . & -")]
        string identifier,
        CancellationToken ct) =>
        await morningstar.GetResearchAsync(identifier, ct);
}
