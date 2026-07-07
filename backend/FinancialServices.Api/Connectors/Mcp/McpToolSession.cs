using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// <see cref="IMcpToolSession"/> over the SDK's <see cref="McpClient"/>. Maps the SDK's tool + content
/// types onto Prism's narrow <see cref="McpToolInfo"/> / <see cref="McpToolCallResult"/> records and
/// logs ids + counts only (P6 — never tool arguments or result text, which may carry licensed data).
/// </summary>
public sealed class McpToolSession(McpClient client, ILogger logger) : IMcpToolSession
{
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var mapped = tools
            .Select(t => new McpToolInfo(t.Name, t.Title, t.Description, t.JsonSchema.GetRawText()))
            .ToArray();

        logger.LogInformation("MCP tools/list returned {Count} tool(s).", mapped.Length);
        return mapped;
    }

    public async Task<McpToolCallResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        var result = await client
            .CallToolAsync(toolName, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var text = string.Join(
            "\n",
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        var structured = result.StructuredContent?.GetRawText();

        logger.LogInformation(
            "MCP tools/call {Tool} completed (isError={IsError}, textLength={TextLength}, hasStructured={HasStructured}).",
            toolName,
            result.IsError ?? false,
            text.Length,
            structured is not null);

        return new McpToolCallResult(result.IsError ?? false, text, structured);
    }

    public async ValueTask DisposeAsync() => await client.DisposeAsync().ConfigureAwait(false);
}
