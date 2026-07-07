using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using FinancialServices.Api.Agents;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Connectors;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Infrastructure.Http;
using FinancialServices.Api.Orchestration;
using FinancialServices.Api.Services;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Infrastructure;

/// <summary>Composition-root helpers for Prism options + real data connectors (architecturalPlan/02, 05).</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds section <c>Prism</c> to <see cref="PrismOptions"/>. Deliberately <b>not</b> gated with
    /// <c>ValidateOnStart</c>: <c>/api/health</c> and synthetic-only runs must boot without a FRED key.
    /// The connectors fail loud at first real use (<see cref="Errors.ConfigurationException"/>) instead.
    /// </summary>
    public static IServiceCollection AddPrismOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PrismOptions>()
            .Bind(configuration.GetSection("Prism"));
        return services;
    }

    /// <summary>
    /// Registers the real EDGAR + FRED typed clients (fixed, allowlisted <c>BaseAddress</c> — SSRF
    /// guard P6/D7 — a bounded <see cref="TransientRetryHandler"/>, and an explicit 15s timeout so a
    /// hung upstream never stalls a sweep) and the three provider-ratings sources. Consumers (pkg
    /// 05/06) inject <c>IEnumerable&lt;IProviderRatingsSource&gt;</c> and select by <c>.Provider</c>.
    /// </summary>
    public static IServiceCollection AddConnectors(this IServiceCollection services)
    {
        // SEC EDGAR — https://data.sec.gov/ only; a descriptive User-Agent is mandatory (else 403).
        services.AddHttpClient<IEdgarClient, EdgarClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                client.BaseAddress = new Uri("https://data.sec.gov/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent", sp.GetRequiredService<IOptions<PrismOptions>>().Value.SecUserAgent);
            })
            .AddHttpMessageHandler(() => new TransientRetryHandler());

        // FRED — https://api.stlouisfed.org/ only (the api_key travels as a query arg per FRED's API).
        services.AddHttpClient<IFredClient, FredClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.stlouisfed.org/");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler(() => new TransientRetryHandler());

        // Provider-ratings sources (multi-registration by interface). MSCI is a labeled-synthetic dev
        // slot; Moody's + Morningstar/DBRS are pending real-API integration and throw until confirmed.
        services.AddSingleton<IProviderRatingsSource, SyntheticRatingsSource>();
        services.AddSingleton<IProviderRatingsSource, MoodysRatingsClient>();
        services.AddSingleton<IProviderRatingsSource, MorningstarDbrsRatingsClient>();

        return services;
    }

    /// <summary>
    /// Registers the live-provider MCP seam (pkg 13, Round 1): the SSRF-guarded
    /// <see cref="Connectors.Mcp.McpToolSessionFactory"/> used by the discovery CLI and (Round 2) the
    /// connectors. Purely additive — nothing on the reconciliation hot path resolves it, and every
    /// provider flag defaults <c>Enabled=false</c>, so registering it never changes runtime behavior
    /// (planning §11 — synthetic stays the default). The per-provider token cache is <b>not</b> a DI
    /// singleton (its path is provider-specific); it is constructed where a provider is authenticated.
    /// </summary>
    public static IServiceCollection AddProviderMcp(this IServiceCollection services)
    {
        services.AddSingleton<Connectors.Mcp.McpToolSessionFactory>();
        return services;
    }

    /// <summary>
    /// Binds section <c>Azure</c> to <see cref="AzureOptions"/> (Cosmos + Search endpoints). Not gated
    /// with <c>ValidateOnStart</c> — <c>/api/health</c> and the unit-test host boot without Azure; the
    /// clients below fail loud at first use.
    /// </summary>
    public static IServiceCollection AddAzureOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureOptions>()
            .Bind(configuration.GetSection("Azure"));
        return services;
    }

    /// <summary>
    /// Registers the real Azure data plane (P1/P6): a shared managed-identity credential
    /// (<see cref="DefaultAzureCredential"/> — no keys), the Cosmos client (camelCase + enum-as-string
    /// serializer so <c>id</c>/<c>issuerId</c>/Provider round-trip), the AI Search client bound to the
    /// ratings index, the stateless deterministic engines, and the reconciliation/audit/corpus services.
    /// The Cosmos + Search factories throw <see cref="ConfigurationException"/> (naming the setting)
    /// when resolved without configuration — never a fabricated endpoint.
    /// </summary>
    public static IServiceCollection AddPrismDataServices(this IServiceCollection services)
    {
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddSingleton(serviceProvider =>
        {
            AzureOptions options = serviceProvider.GetRequiredService<IOptions<AzureOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.CosmosEndpoint))
            {
                throw new ConfigurationException("Azure:CosmosEndpoint", "Cosmos DB endpoint is not configured.");
            }

            return new CosmosClient(
                options.CosmosEndpoint,
                serviceProvider.GetRequiredService<TokenCredential>(),
                new CosmosClientOptions
                {
                    ApplicationName = "prism-api",
                    UseSystemTextJsonSerializerWithOptions = PrismJson.Options,
                });
        });

        services.AddSingleton(serviceProvider =>
        {
            AzureOptions options = serviceProvider.GetRequiredService<IOptions<AzureOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.SearchEndpoint))
            {
                throw new ConfigurationException("Azure:SearchEndpoint", "AI Search endpoint is not configured.");
            }

            return new SearchClient(
                new Uri(options.SearchEndpoint),
                options.SearchIndex,
                serviceProvider.GetRequiredService<TokenCredential>());
        });

        // Deterministic engines (stateless, P2) + wall clock (testable) + data/orchestration services.
        services.AddSingleton<DivergenceDecomposer>();
        services.AddSingleton<RedFlagEngine>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ISearchCorpus, SearchCorpus>();
        services.AddSingleton<ICosmosDossierStore, CosmosDossierStore>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IReconciliationService, ReconciliationService>();

        return services;
    }

    /// <summary>
    /// Registers the pkg-06 Foundry narration layer (P1/P2/P6): the single Agent Framework SDK seam
    /// (<see cref="IAgentTextRunner"/> → <see cref="AzureAgentRunner"/>, inline agents over the GA
    /// <c>Microsoft.Agents.AI[.OpenAI]</c> — no portal agents, no prerelease SDKs), the four
    /// narrator/retriever wrappers, and the <see cref="IDossierNarrator"/> that fills the dossier's
    /// narrative fields. All are <b>singletons</b>: they are stateless, and <see cref="ReconciliationService"/>
    /// (a singleton) consumes <see cref="IDossierNarrator"/> — singleton dependencies avoid a captive
    /// dependency (a deliberate, documented refinement of arch-04's "scoped", consistent with the rest of
    /// this composition root). The runner fails loud (<see cref="ConfigurationException"/>) at first use
    /// when <c>Azure:OpenAiEndpoint</c> is unset — never a fabricated endpoint.
    /// </summary>
    public static IServiceCollection AddPrismAgents(this IServiceCollection services)
    {
        services.AddSingleton<IAgentTextRunner, AzureAgentRunner>();
        services.AddSingleton<ProviderExplainerAgent>();
        services.AddSingleton<FundamentalsAgent>();
        services.AddSingleton<RedFlagNarratorAgent>();
        services.AddSingleton<DivergenceNarratorAgent>();
        services.AddSingleton<IDossierNarrator, DossierNarrator>();

        return services;
    }

    /// <summary>
    /// Registers the pkg-07 orchestration layer (spec §A/§D): the shared transport-agnostic sweep steps,
    /// the SSE streaming orchestrator (the guaranteed-green fallback transport), the live AG-UI agent
    /// orchestrator, and the AG-UI hosting services (<c>AddAGUI</c>). All are stateless singletons. The
    /// AG-UI endpoint itself is mapped conditionally in <c>Program.cs</c> on <c>Prism:AgUiEnabled</c>, so
    /// registering these services never forces the prerelease hosting package to run at boot (P1).
    /// </summary>
    public static IServiceCollection AddPrismOrchestration(this IServiceCollection services)
    {
        services.AddSingleton<PrismSweepSteps>();
        services.AddSingleton<PrismStreamingOrchestrator>();
        services.AddSingleton<PrismAgentOrchestrator>();

        // AG-UI DI services (Microsoft.Agents.AI.Hosting.AGUI.AspNetCore, prerelease). Registration is
        // inert until MapAGUI is called; the mapping is gated on Prism:AgUiEnabled.
        services.AddAGUI();

        return services;
    }
}
