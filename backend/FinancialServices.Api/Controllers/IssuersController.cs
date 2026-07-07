using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

/// <summary>
/// The issuer cast, read from the real AI Search corpus (arch-09). Thin — delegates to
/// <see cref="ISearchCorpus"/>. Reads are <c>[AllowAnonymous]</c> for the hackathon demo (spec §A).
/// <b>Path to customer build:</b> add JWT-bearer auth (Entra), an <c>[Authorize]</c> default policy,
/// and per-issuer authorization — never ship anonymous with real PII.
/// </summary>
[ApiController]
[Route("api/v1/issuers")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class IssuersController(ISearchCorpus corpus) : ControllerBase
{
    /// <summary>Lists the issuer cast (id, name, sector, ticker, coverage).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IssuerListItem>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IssuerListItem>> GetAll(CancellationToken ct)
    {
        IReadOnlyList<IssuerCorpusEntry> entries = await corpus.GetIssuersAsync(ct);
        return entries.Select(entry => entry.Issuer.ToListItem(entry.Coverage)).ToArray();
    }

    /// <summary>Fetches one issuer and the providers that cover it; 404 if unknown.</summary>
    [HttpGet("{issuerId}")]
    [ProducesResponseType<IssuerListItem>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorBody>(StatusCodes.Status404NotFound)]
    public async Task<IssuerListItem> GetById(string issuerId, CancellationToken ct)
    {
        IssuerCorpusEntry entry = await corpus.GetIssuerAsync(issuerId, ct)
            ?? throw new NotFoundException("issuer", issuerId);
        return entry.Issuer.ToListItem(entry.Coverage);
    }
}
