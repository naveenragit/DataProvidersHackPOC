using System.Text;
using System.Text.Json;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Orchestration;
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
    IReconciliationService service,
    IAuditService audit,
    TimeProvider clock,
    PrismStreamingOrchestrator orchestrator,
    ILogger<ReconciliationsController> logger) : ControllerBase
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

    /// <summary>
    /// Streams the reconciliation sweep as Server-Sent Events (spec §D): the guaranteed-green transport
    /// behind the Evidence Stream. Emits <c>scope-confirm</c> (the P5 gate — the sweep proceeds only when
    /// <c>Confirmed</c> is true), then a card per step (provider ratings, fundamentals, divergence
    /// waterfall, red flags) and finally <c>dossier-ready</c> with the persisted dossier (identical to
    /// <c>POST</c> above). Tool arguments are re-authorized in the orchestrator (P6); a fault is surfaced
    /// in-band as an <c>error</c> event rather than tearing the stream (arch-03).
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ReconciliationStreamRequest request, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // defeat proxy buffering so events flush live.

        async Task Emit(string type, object payload, CancellationToken c)
        {
            string json = JsonSerializer.Serialize(payload, payload.GetType(), PrismJson.Options);
            byte[] frame = Encoding.UTF8.GetBytes($"data: {{\"type\":\"{type}\",\"payload\":{json}}}\n\n");
            await Response.Body.WriteAsync(frame, c);
            await Response.Body.FlushAsync(c);
        }

        try
        {
            await orchestrator.RunAsync(request.IssuerId, request.AsOf!.Value, request.Confirmed, Emit, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client navigated away / aborted the fetch — expected, nothing more to emit.
        }
        catch (NotFoundException ex)
        {
            await SafeEmitErrorAsync(Emit, ex.Code, ex.Message, ct);
        }
        catch (ValidationException ex)
        {
            await SafeEmitErrorAsync(Emit, ex.Code, ex.Message, ct);
        }
        catch (Exception ex)
        {
            // ids + counts only (P6) — never the payload/financials.
            logger.LogError(ex, "Reconciliation stream failed for {IssuerId}", request.IssuerId);
            await SafeEmitErrorAsync(Emit, "INTERNAL", "The reconciliation stream failed.", ct);
        }
    }

    // Emits an in-band error event; swallows a secondary write fault (the client is likely gone).
    private static async Task SafeEmitErrorAsync(
        Func<string, object, CancellationToken, Task> emit, string code, string message, CancellationToken ct)
    {
        try
        {
            await emit(PrismStreamEventTypes.Error, new StreamErrorPayload(code, message), ct);
        }
        catch
        {
            // The response stream is already closed — nothing else to do.
        }
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
