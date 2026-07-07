namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>A tool advertised by a provider's MCP server (<c>tools/list</c>), normalized for Prism.</summary>
/// <param name="Name">The tool id used in <c>tools/call</c>.</param>
/// <param name="Title">Human title, if the server supplied one.</param>
/// <param name="Description">Free-text description, if any.</param>
/// <param name="JsonSchema">The tool's input JSON schema as raw JSON (for the discovery findings note).</param>
public sealed record McpToolInfo(string Name, string? Title, string Description, string JsonSchema);

/// <summary>The outcome of a <c>tools/call</c>, normalized for Prism.</summary>
/// <param name="IsError">Whether the server flagged the result as an error.</param>
/// <param name="Text">Concatenated text content blocks (may be empty).</param>
/// <param name="StructuredJson">The structured content as raw JSON, if the tool returned any.</param>
public sealed record McpToolCallResult(bool IsError, string Text, string? StructuredJson);

/// <summary>
/// A connected MCP tool session over a provider's Streamable-HTTP endpoint. Wraps the SDK's
/// <c>McpClient</c> so Prism code depends on a narrow, fakeable seam (the discovery CLI and the
/// Round-2 connectors both use this). The SDK performs the <c>initialize</c> →
/// <c>notifications/initialized</c> handshake and owns the <c>Mcp-Session-Id</c> + negotiated protocol
/// version (STK-04); this wrapper only lists/calls tools and logs ids + counts (P6). <c>ct</c> is
/// plumbed through every call (P7).
/// </summary>
public interface IMcpToolSession : IAsyncDisposable
{
    /// <summary>Lists the server's tools (<c>tools/list</c>).</summary>
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken);

    /// <summary>Calls one tool (<c>tools/call</c>) with the given arguments.</summary>
    Task<McpToolCallResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
