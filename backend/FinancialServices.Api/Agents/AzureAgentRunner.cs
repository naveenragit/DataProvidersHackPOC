using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Core;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace FinancialServices.Api.Agents;

/// <summary>
/// The production <see cref="IAgentTextRunner"/> — the single touchpoint to the Azure AI Agent
/// Framework (GA <c>Microsoft.Agents.AI[.OpenAI]</c> 1.13.0 over <c>Azure.AI.OpenAI</c> 2.1.0).
/// Agents are created <b>inline</b> from instructions (no Foundry-portal agents, no prerelease
/// <c>Azure.AI.Projects</c>) and bound to the configured <c>gpt-*</c> deployment (arch-04: models are
/// configurable, never hardcoded). Auth is Entra ID only (<see cref="DefaultAzureCredential"/> via the
/// shared DI <see cref="TokenCredential"/>) — no keys (P6). One <see cref="AzureOpenAIClient"/> is
/// cached; a fresh <see cref="AIAgent"/> is created per call (cheap, stateless).
/// </summary>
public sealed class AzureAgentRunner(
    IOptions<AzureOptions> options,
    TokenCredential credential,
    ILogger<AzureAgentRunner> logger) : IAgentTextRunner
{
    private static readonly ActivitySource Activity = new("FinancialServices.Agents");
    private readonly AzureOptions _options = options.Value;
    private readonly object _gate = new();
    private AzureOpenAIClient? _client;

    public async Task<string> RunAsync(
        string deploymentName, string agentName, string instructions, string prompt, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using Activity? span = Activity.StartActivity("Agent.Run");
        span?.SetTag("prism.agent", agentName);
        span?.SetTag("prism.model", deploymentName);

        AIAgent agent = CreateAgent(deploymentName, agentName, instructions);

        // ids + lengths only — never the prompt body or the model output (P6).
        logger.LogInformation(
            "Agent {Agent} ({Model}) invoked ({PromptChars} prompt chars)",
            agentName, deploymentName, prompt.Length);

        // Typed as var: on the base AIAgent the run response type varies by SDK line; we only read .Text.
        var response = await agent.RunAsync(prompt, cancellationToken: ct);
        string text = response.Text ?? string.Empty;

        logger.LogInformation("Agent {Agent} returned {Length} chars", agentName, text.Length);
        return text;
    }

    // Inline agent creation via the GA Agent Framework AsAIAgent extension. Chat Completions is the GA
    // surface exposed by Azure.AI.OpenAI 2.1.0 and is correct for the pkg-06 narrators (no hosted
    // tools). The Responses client (hosted tools) is a pkg-07 concern and is not on this GA pin, so an
    // explicit Responses request fails loud (P1 — no silent downgrade) rather than pretending.
    private AIAgent CreateAgent(string deploymentName, string agentName, string instructions)
    {
        if (_options.AgentApi.Equals("Responses", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(
                "Azure:AgentApi",
                "The Responses client is not enabled on the GA Azure.AI.OpenAI 2.1.0 pin (it lands with " +
                "the pkg-07 orchestrator). Set Azure:AgentApi=ChatCompletions.");
        }

        return GetClient()
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: instructions, name: agentName);
    }

    private AzureOpenAIClient GetClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        lock (_gate)
        {
            if (_client is null)
            {
                if (string.IsNullOrWhiteSpace(_options.OpenAiEndpoint))
                {
                    throw new ConfigurationException(
                        "Azure:OpenAiEndpoint",
                        "Azure OpenAI endpoint is not configured for the Foundry narration agents.");
                }

                _client = new AzureOpenAIClient(new Uri(_options.OpenAiEndpoint), credential);
            }

            return _client;
        }
    }
}
