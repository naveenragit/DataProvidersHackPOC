namespace FinancialServices.Api.Infrastructure;

/// <summary>
/// Azure resource configuration (section <c>Azure</c>, from <c>Azure__*</c> env vars — see
/// <c>.env.example</c> and architecturalPlan/05). Bound loosely (P1): <c>/api/health</c> and the
/// unit-test host boot without these set; the Cosmos + Search clients fail <b>loud at first use</b>
/// (<see cref="Errors.ConfigurationException"/> naming the missing setting) rather than fabricating a
/// placeholder endpoint.
/// </summary>
public sealed class AzureOptions
{
    /// <summary>Cosmos DB account endpoint, e.g. <c>https://cosmos-dataproviders-poc.documents.azure.com:443/</c>.</summary>
    public string CosmosEndpoint { get; init; } = "";

    /// <summary>Cosmos database name. Prism uses <c>prism</c> (containers <c>rating_reconciliations</c> + <c>audit_events</c>).</summary>
    public string CosmosDatabase { get; init; } = "prism";

    /// <summary>Azure AI Search endpoint, e.g. <c>https://aisearch-dataproviders-poc.search.windows.net</c>.</summary>
    public string SearchEndpoint { get; init; } = "";

    /// <summary>The ratings corpus index name (pkg 03). Defaults to <c>prism-ratings</c>.</summary>
    public string SearchIndex { get; init; } = "prism-ratings";

    /// <summary>
    /// Azure OpenAI (Foundry) endpoint for the pkg-06 narration agents, e.g.
    /// <c>https://foundry-dataproviders-poc.openai.azure.com/</c> — the dedicated <c>.openai</c> host
    /// (the <c>services.ai</c> host is not accepted by the Azure OpenAI SDK). Empty at boot; the agent
    /// runner throws <see cref="Errors.ConfigurationException"/> at first use (P1) — never a placeholder.
    /// </summary>
    public string OpenAiEndpoint { get; init; } = "";

    /// <summary>
    /// Which Azure OpenAI client surface the agents use: <c>ChatCompletions</c> (default — broad
    /// compatibility, no hosted tools; correct for the pkg-06 narrators) or <c>Responses</c> (reserved
    /// for the pkg-07 tool-using orchestrator). Any other value falls back to Chat Completions.
    /// </summary>
    public string AgentApi { get; init; } = "ChatCompletions";
}
