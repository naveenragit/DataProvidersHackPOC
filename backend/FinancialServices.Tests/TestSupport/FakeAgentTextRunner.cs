using FinancialServices.Api.Agents;

namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// A test-only <see cref="IAgentTextRunner"/> (the pkg-06 SDK seam). It never touches Azure, so the
/// narrator + <see cref="NarrationGuard"/> pipeline is exercised fully offline (P1 — the fake lives in
/// test code, never in a runtime path). The <paramref name="respond"/> delegate receives the grounded
/// prompt so a test can echo the facts (guard accepts) or mutate them (guard drops).
/// </summary>
public sealed class FakeAgentTextRunner(Func<string, string> respond) : IAgentTextRunner
{
    /// <summary>Number of times <see cref="RunAsync"/> was invoked — asserts the "disabled" short-circuit.</summary>
    public int Calls { get; private set; }

    public Task<string> RunAsync(
        string deploymentName, string agentName, string instructions, string prompt, CancellationToken ct)
    {
        Calls++;
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(respond(prompt));
    }

    /// <summary>Echoes the grounded prompt verbatim — every reference + number is present, so the guard accepts.</summary>
    public static FakeAgentTextRunner Echo() => new(prompt => prompt);

    /// <summary>Always throws — models a permanent fault such as a misconfiguration.</summary>
    public static FakeAgentTextRunner Throws(Exception error) => new(_ => throw error);
}
