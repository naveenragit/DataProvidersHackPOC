// Copy to backend/FinancialServices.Api/Program.cs
// ASP.NET Core (.NET 9) Web API host for the financial services platform.
using FinancialServices.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Strongly-typed options — validated at startup. Missing required settings fail fast
// (no silent fallbacks / fake values). Env vars bind via the AZURE__ prefix.
builder.Services.AddOptions<AzureOptions>()
    .Bind(builder.Configuration.GetSection("Azure"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// AuthN: Microsoft Entra ID (JWT bearer). Financial endpoints are NEVER anonymous.
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// AuthZ: deny-by-default — every endpoint requires an authenticated caller unless it opts out
// with [AllowAnonymous]. Layer object-level authorization (advisorId/clientId scoping) in services.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Azure clients (singletons), domain services + agents (scoped), and telemetry.
builder.Services.AddAzureClients();
builder.Services.AddDomainServices();
builder.Services.AddAppTelemetry(builder.Configuration);

// CORS — specific origins only (frontend + CopilotKit sidecar). Never AllowAnyOrigin in prod.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:5173", "http://localhost:4000"];
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

// Enables WebApplicationFactory<Program> in the xUnit integration test project.
public partial class Program;
