using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

/// <summary>
/// The fallback (synchronous) reconciliation surface + persisted-dossier fetch/export (arch-09, spec
/// §A). Thin — delegates to <see cref="IReconciliationService"/>. <c>[AllowAnonymous]</c> for the demo;
/// the Entra bearer pattern is the path to customer build (see <see cref="IssuersController"/>).
/// </summary>
[ApiController]
[Route("api/v1/reconciliations")]
[AllowAnonymous]
public sealed class ReconciliationsController(
    IReconciliationService service, IAuditService audit, TimeProvider clock) : ControllerBase
{
    /// <summary>Runs the deterministic fallback sweep and persists the dossier.</summary>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType<DossierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorBody>(StatusCodes.Status400BadRequest)]
    public async Task<DossierResponse> Post([FromBody] ReconciliationRequest request, CancellationToken ct)
    {
        // [ApiController] enforces DataAnnotations before this runs, so IssuerId/AsOf are non-null here.
        ReconciliationDossier dossier = await service.RunAsync(request.IssuerId!, request.AsOf!.Value, ct);
        return dossier.ToResponse();
    }

    /// <summary>Fetches a persisted dossier by id (the id encodes the partition key); 404 if unknown.</summary>
    [HttpGet("{id}")]
    [Produces("application/json")]
    [ProducesResponseType<DossierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorBody>(StatusCodes.Status404NotFound)]
    public async Task<DossierResponse> GetById(string id, CancellationToken ct)
    {
        string issuerId = PartitionOf(id);
        ReconciliationDossier dossier = await service.GetAsync(id, issuerId, ct)
            ?? throw new NotFoundException("dossier", id);
        return dossier.ToResponse();
    }

    /// <summary>Renders a persisted dossier as printable HTML (PDF fallback is client-side, pkg 10).</summary>
    [HttpGet("{id}/export")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorBody>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(string id, CancellationToken ct)
    {
        string issuerId = PartitionOf(id);
        ReconciliationDossier dossier = await service.GetAsync(id, issuerId, ct)
            ?? throw new NotFoundException("dossier", id);

        await audit.WriteAsync(
            new AuditEvent(
                Id: $"{issuerId}:{Guid.NewGuid():N}",
                EventType: "reconciliation",
                Timestamp: clock.GetUtcNow(),
                Actor: "system",
                IssuerId: issuerId,
                Action: "dossier_exported",
                Metadata: new Dictionary<string, object> { ["dossierId"] = id }),
            ct);

        return Content(DossierHtmlRenderer.Render(dossier), "text/html");
    }

    // Recover the partition key from "{issuerId}:{guid}". A malformed id is a client error (400).
    private static string PartitionOf(string id)
    {
        int separator = id.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new ValidationException(
                "Malformed dossier id.", new Dictionary<string, object?> { ["id"] = id });
        }

        return id[..separator];
    }
}
