// Prism API host. Minimal scaffold — expanded per implementationPlan packages
// (options + DI + AG-UI + controllers) as those capabilities are implemented.
using FinancialServices.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Prism options (section "Prism", validated at startup — P1 fail-loud) + real data connectors
// (EDGAR/FRED typed clients + provider-ratings sources). See architecturalPlan/05 + implementationPlan/04.
builder.Services.AddPrismOptions(builder.Configuration).AddConnectors();

var app = builder.Build();

app.MapHealthChecks("/api/health");
app.MapGet("/", () => Results.Ok(new { service = "Prism", status = "ok" }));

app.Run();

// Enables WebApplicationFactory<Program> in the xUnit integration test project.
public partial class Program;
