namespace FinancialServices.Api.Agents;

/// <summary>
/// The single seam over the Azure AI Agent Framework SDK (pkg 06). Every narrator/retriever agent
/// depends on this, not on the SDK directly, so the deterministic-plus-narration pipeline is fully
/// unit-testable offline with a test-only fake (P1 — no mocks in runtime code). The production
/// implementation (<see cref="AzureAgentRunner"/>) is the <b>only</b> class that references
/// <c>Microsoft.Agents.AI</c> / <c>Azure.AI.OpenAI</c>.
/// </summary>
public interface IAgentTextRunner
{
    /// <summary>
    /// Runs an inline Azure AI Foundry agent (created from <paramref name="instructions"/> + bound to
    /// the <paramref name="deploymentName"/> model) with a single grounded <paramref name="prompt"/>
    /// and returns its text. The caller supplies all grounding in the prompt (arch-04: retrieve in
    /// code, never let the model free-roam) and post-validates the result (see
    /// <see cref="NarrationGuard"/>).
    /// </summary>
    /// <param name="deploymentName">The Azure OpenAI deployment (from <c>Prism__Models__*</c>).</param>
    /// <param name="agentName">A stable agent name (telemetry + the SDK agent name).</param>
    /// <param name="instructions">The system/instructions text that keeps the agent grounded and citation-only.</param>
    /// <param name="prompt">The user turn — the already-retrieved facts to narrate.</param>
    /// <param name="ct">Cancellation (P7).</param>
    Task<string> RunAsync(
        string deploymentName, string agentName, string instructions, string prompt, CancellationToken ct);
}
