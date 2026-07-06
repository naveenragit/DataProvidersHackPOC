using FinancialServices.Api.Connectors;
using FinancialServices.Api.Infrastructure.Http;
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
}
