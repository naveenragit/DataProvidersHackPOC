// Prism API host. Options + connectors (pkg 04/05) + Azure data services, controllers, the standard
// error envelope, and OpenAPI (pkg 08). AG-UI streaming (pkg 07) and auth (pkg 07/11) land later.
using System.Text.Json.Serialization;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Http;
using FinancialServices.Api.Models;
using FinancialServices.Api.Orchestration;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Prism options (section "Prism") + real data connectors (pkg 04) and Azure data plane (pkg 08:
// Cosmos + AI Search). Missing config fails loud at first use, not at boot (P1) — health stays green.
builder.Services
    .AddPrismOptions(builder.Configuration)
    .AddConnectors()
    .AddProviderMcp();
builder.Services
    .AddAzureOptions(builder.Configuration)
    .AddPrismDataServices()
    .AddPrismAgents()
    .AddPrismOrchestration();

// Controllers + the JSON wire contract: camelCase (Web default) + enums as their member NAMES (the
// cross-package DTO contract, e.g. Provider → "Msci") + reject unknown request fields (arch-09).
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

// Model-validation failures (missing/blank required field, unknown field) use the standard Prism
// error envelope (arch-03), not the default ProblemDetails shape.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => (object?)string.Join("; ", entry.Value!.Errors.Select(error => error.ErrorMessage)));

        var body = new ApiErrorBody(new ApiError(
            "VALIDATION_FAILED", "One or more validation errors occurred.", details));
        return new ObjectResult(body) { StatusCode = StatusCodes.Status400BadRequest };
    };
});

// One exception boundary → the standard { error: { code, message, details } } envelope.
builder.Services.AddExceptionHandler<PrismExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

// CORS for the Vite dev origin (arch-06: specific origins, never "*").
const string DevCorsPolicy = "prism-dev";
builder.Services.AddCors(options => options.AddPolicy(
    DevCorsPolicy,
    policy => policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors(DevCorsPolicy);

app.MapHealthChecks("/api/health");
app.MapGet("/", () => Results.Ok(new { service = "Prism", status = "ok" }));
app.MapControllers();

// Live AG-UI orchestrator endpoint (pkg 07 §A), consumed by the CopilotKit sidecar. Gated on
// Prism:AgUiEnabled (default false) so the prerelease hosting package never gates a bare boot / the
// test host (P1); the SSE stream + REST endpoints are the always-on demo path. The agent is built
// eagerly here — it fails loud if Azure:OpenAiEndpoint is unset (P1), which is the intended contract
// when AG-UI is explicitly enabled.
if (app.Services.GetRequiredService<IOptions<PrismOptions>>().Value.AgUiEnabled)
{
    app.MapAGUI("/prism", app.Services.GetRequiredService<PrismAgentOrchestrator>().Agent);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

// Enables WebApplicationFactory<Program> in the xUnit integration test project.
public partial class Program;
