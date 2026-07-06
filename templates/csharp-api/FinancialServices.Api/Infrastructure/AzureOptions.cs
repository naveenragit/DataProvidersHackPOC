// Copy to backend/FinancialServices.Api/Infrastructure/AzureOptions.cs
using System.ComponentModel.DataAnnotations;

namespace FinancialServices.Api.Infrastructure;

/// <summary>
/// Strongly-typed Azure configuration. Bound from the "Azure" config section /
/// AZURE__* environment variables. Never read raw env vars in business logic.
/// </summary>
public sealed class AzureOptions
{
    // Azure AI Foundry
    [Required] public required string AiProjectEndpoint { get; init; }
    [Required] public required string AiProjectName { get; init; }
    public string AgentModel { get; init; } = "gpt-4o";

    // Azure OpenAI (used by the CopilotKit Node runtime sidecar)
    [Required] public required string OpenAiEndpoint { get; init; }
    public string OpenAiApiVersion { get; init; } = "2024-12-01-preview";

    // Azure Cosmos DB
    [Required] public required string CosmosEndpoint { get; init; }
    [Required] public required string CosmosDatabase { get; init; }

    // Azure AI Search
    [Required] public required string SearchEndpoint { get; init; }
    [Required] public required string SearchIndex { get; init; }

    // Azure AI Content Safety
    public string? ContentSafetyEndpoint { get; init; }
}
