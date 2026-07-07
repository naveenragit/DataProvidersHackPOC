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
}
