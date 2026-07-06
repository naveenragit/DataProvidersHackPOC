namespace FinancialServices.Api.Infrastructure;

/// <summary>
/// Prism-specific configuration (section <c>Prism</c>), bound once (architecturalPlan/05). Secrets are
/// <b>not</b> gated at boot: <c>/api/health</c> and synthetic-only runs start without a FRED key. The
/// real connectors fail <b>loud at the point of first use</b> (P1) via
/// <see cref="Errors.ConfigurationException"/> naming the missing setting — never a placeholder/fake.
/// </summary>
public sealed class PrismOptions
{
    /// <summary>
    /// FRED API key (free). Optional at boot; <c>FredClient</c> throws a
    /// <see cref="Errors.ConfigurationException"/> on first real call if it is unset.
    /// </summary>
    public string? FredApiKey { get; init; }

    /// <summary>
    /// Descriptive SEC User-Agent (SEC returns 403 without one). Has a sensible default so boot never
    /// fails; override in config for real fetches. <c>EdgarClient</c> rejects a blank value at use.
    /// </summary>
    public string SecUserAgent { get; init; } = "Prism/1.0 (Data Providers hackathon; set Prism:SecUserAgent)";

    /// <summary>Per-agent model deployment names (surfaced in the Settings page).</summary>
    public ModelOptions Models { get; init; } = new();

    /// <summary>
    /// Pending real-provider API placeholders (Moody's / Morningstar DBRS). All nullable and
    /// <b>never</b> <c>[Required]</c>: the options exist so the wiring is honest, but no endpoint or
    /// auth value is invented until the product spec is confirmed (P1 — no fabrication).
    /// </summary>
    public ProviderApiOptions ProviderApis { get; init; } = new();

    /// <summary>Deployment names for each agent role; defaults mirror <c>.env.example</c> + architecturalPlan/05.</summary>
    public sealed class ModelOptions
    {
        public string Orchestrator { get; init; } = "gpt-5";
        public string Provider { get; init; } = "gpt-4.1-mini";
        public string Fundamentals { get; init; } = "gpt-4.1-mini";
        public string RedFlag { get; init; } = "gpt-5-mini";
    }

    /// <summary>Nullable placeholders for the pending Moody's / Morningstar DBRS APIs.</summary>
    public sealed class ProviderApiOptions
    {
        public ProviderApiEndpoint? MoodysApi { get; init; }
        public ProviderApiEndpoint? MorningstarApi { get; init; }
    }

    /// <summary>A pending provider endpoint. Values stay <c>null</c> until the real spec lands.</summary>
    public sealed class ProviderApiEndpoint
    {
        public string? BaseUrl { get; init; }
        public string? ApiKey { get; init; }
    }
}
