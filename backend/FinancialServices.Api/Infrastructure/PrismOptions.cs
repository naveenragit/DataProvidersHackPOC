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
    /// Feature flag for the pkg-06 Foundry narration step. Default <c>true</c> (real services, P1); a
    /// narration failure is caught and the deterministic dossier is returned regardless (the narrative
    /// fields stay empty and the verbatim rule text carries the result). Set <c>false</c> to skip
    /// Foundry entirely and return deterministic-only dossiers.
    /// </summary>
    public bool NarrationEnabled { get; init; } = true;

    /// <summary>
    /// Opt-in flag for the pkg-07 live AG-UI agent host (<c>MapAGUI("/prism")</c>). Default <c>false</c>:
    /// the guaranteed-green SSE streaming fallback (<c>POST /api/v1/reconciliations/stream</c>) and the
    /// atomic REST endpoint carry the demo, so the prerelease AG-UI hosting package never gates the
    /// unit-test host or a bare boot. Set <c>true</c> (with a reachable Azure OpenAI endpoint) to expose
    /// the AG-UI endpoint for the CopilotKit sidecar.
    /// </summary>
    public bool AgUiEnabled { get; init; }

    /// <summary>
    /// Pending real-provider API placeholders (Moody's / Morningstar DBRS). All nullable and
    /// <b>never</b> <c>[Required]</c>: the options exist so the wiring is honest, but no endpoint or
    /// auth value is invented until the product spec is confirmed (P1 — no fabrication).
    /// </summary>
    public ProviderApiOptions ProviderApis { get; init; } = new();

    /// <summary>
    /// Live rating-provider MCP servers (Morningstar + Moody's), bound from <c>Prism:Providers:*</c>
    /// (pkg 13). Every provider defaults to <see cref="ProviderMcpOptions.Enabled"/> <c>= false</c>, so
    /// with no config the app stays on the labeled-synthetic corpus (planning §11 — synthetic is the
    /// permanent fallback). No boot gate: a misconfigured/enabled provider fails <b>loud</b> at the
    /// one-time-login / ingestion boundary (never on the demo hot path).
    /// </summary>
    public ProvidersOptions Providers { get; init; } = new();

    /// <summary>Deployment names for each agent role; defaults mirror <c>.env.example</c> + architecturalPlan/05.</summary>
    public sealed class ModelOptions
    {
        // Defaults target the only model deployed on the Foundry account (gpt-5.4). Override per role
        // via Prism:Models:* when smaller/cheaper deployments (e.g. gpt-4.1-mini) become available.
        public string Orchestrator { get; init; } = "gpt-5.4";
        public string Provider { get; init; } = "gpt-5.4";
        public string Fundamentals { get; init; } = "gpt-5.4";
        public string RedFlag { get; init; } = "gpt-5.4";
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

    /// <summary>Live MCP rating providers (pkg 13). Each is independently flag-gated.</summary>
    public sealed class ProvidersOptions
    {
        /// <summary>Morningstar/DBRS hosted MCP server (<c>https://mcp.morningstar.com/mcp</c>).</summary>
        public ProviderMcpOptions Morningstar { get; init; } = new();

        /// <summary>Moody's hosted MCP server (<c>https://api.moodys.com/genai-ready-data/m1/mcp</c>).</summary>
        public ProviderMcpOptions Moodys { get; init; } = new();
    }

    /// <summary>
    /// One live provider's MCP + OAuth 2.1 settings. <see cref="Enabled"/> defaults <c>false</c> and
    /// the secrets stay <c>null</c> until the operator fills a git-ignored <c>.env</c> (P6 — the
    /// committed <c>.env.example</c> never carries a real <see cref="ClientSecret"/>). Validation of
    /// <see cref="McpUrl"/> / <see cref="RedirectUri"/> happens at first real use (fail loud), not boot.
    /// </summary>
    public sealed class ProviderMcpOptions
    {
        /// <summary>Master switch. <c>false</c> (default) ⇒ this provider is off and synthetic is used.</summary>
        public bool Enabled { get; init; }

        /// <summary>The hosted MCP endpoint (Streamable HTTP). Host-allowlisted at use (SSRF, SEC-03).</summary>
        public string? McpUrl { get; init; }

        /// <summary>Pre-registered OAuth <c>client_id</c> (public identifier — not a secret).</summary>
        public string? ClientId { get; init; }

        /// <summary>Pre-registered OAuth <c>client_secret</c> (P6 — never logged, never committed).</summary>
        public string? ClientSecret { get; init; }

        /// <summary>The allow-listed loopback redirect (default <c>http://localhost:8765/callback</c>).</summary>
        public string? RedirectUri { get; init; }
    }
}
