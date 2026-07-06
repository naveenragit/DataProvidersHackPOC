// Copy to backend/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs
using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Identity;
using FinancialServices.Api.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Agents;

/// <summary>
/// PortfolioAnalysisAgent: analyzes portfolio composition and market context using an
/// Azure AI Foundry agent invoked through the Microsoft Agent Framework (.NET).
///
/// IMPORTANT — verify the SDK surface before building. The Foundry agent retrieval extension
/// (GetAIAgent) is provided by the Microsoft.Agents.AI.AzureAI package and takes an agent id;
/// the run method and response type (e.g. RunAsync / AgentRunResponse vs AgentResponse) vary by
/// package version. Align the calls below with your installed Microsoft.Agents.AI[.AzureAI].
/// Agents are DEFINED in Azure AI Foundry and referenced here by id/name (never recreated).
/// </summary>
public sealed class PortfolioAnalysisAgent(
    IOptions<AzureOptions> options,
    ILogger<PortfolioAnalysisAgent> logger)
{
    private static readonly ActivitySource Activity = new("FinancialServices.Agents");
    private readonly AzureOptions _options = options.Value;

    public async Task<string> AnalyzeAsync(
        string portfolioSummary, string marketContext, CancellationToken ct)
    {
        using var span = Activity.StartActivity("PortfolioAnalysisAgent.Analyze");

        var projectClient = new AIProjectClient(
            new Uri(_options.AiProjectEndpoint), new DefaultAzureCredential());

        AIAgent agent = await projectClient.GetAIAgentAsync(
            "PortfolioAnalysisAgent", cancellationToken: ct);

        AgentRunResponse response = await agent.RunAsync(
            $"Analyze portfolio:\n{portfolioSummary}\n\nMarket context:\n{marketContext}",
            cancellationToken: ct);

        logger.LogInformation("PortfolioAnalysisAgent completed ({Length} chars)", response.Text.Length);
        return response.Text;
    }
}
