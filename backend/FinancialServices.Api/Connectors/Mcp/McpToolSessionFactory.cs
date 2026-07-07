using Microsoft.Extensions.Logging;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// Opens <see cref="IMcpToolSession"/>s to a provider's hosted MCP server. Every endpoint is
/// SSRF-validated (<see cref="ProviderMcpEndpointGuard"/>) before a socket is opened. Two paths:
/// <list type="bullet">
/// <item><see cref="ConnectAsync"/> — production/CLI: the SDK owns the <see cref="System.Net.Http.HttpClient"/>
/// and the OAuth pipeline (discovery + PKCE + refresh) via the supplied <see cref="ClientOAuthOptions"/>;</item>
/// <item><see cref="ConnectWithHttpClientAsync"/> — offline tests: an injected
/// <see cref="System.Net.Http.HttpClient"/> (fake handler) serves the JSON-RPC, no OAuth, no live network.</item>
/// </list>
/// The protocol version is <b>never hand-pinned</b> — <see cref="McpClient.CreateAsync"/> negotiates it
/// at <c>initialize</c> (STK-04). <c>ct</c> is plumbed throughout (P7).
/// </summary>
public sealed class McpToolSessionFactory(ILoggerFactory loggerFactory)
{
    private static readonly McpClientOptions ClientOptions = new()
    {
        ClientInfo = new Implementation { Name = "prism-provider-mcp", Version = "1.0.0" },
    };

    /// <summary>
    /// Connects to <paramref name="mcpUrl"/> for <paramref name="provider"/> with the given OAuth
    /// options. The endpoint is host-allowlisted first (SEC-03); the SDK handles the
    /// <c>initialize</c>/<c>initialized</c> handshake and bearer auth.
    /// </summary>
    public async Task<IMcpToolSession> ConnectAsync(
        ProviderMcpKey provider,
        string? mcpUrl,
        ClientOAuthOptions oauthOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(oauthOptions);
        var endpoint = ProviderMcpEndpointGuard.EnsureAllowedMcpUrl(provider, mcpUrl);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            OAuth = oauthOptions,
        };

        var transport = new HttpClientTransport(transportOptions, loggerFactory);
        return await ConnectCoreAsync(transport, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Test/no-auth seam: connects over an injected <see cref="System.Net.Http.HttpClient"/> (the
    /// endpoint is still host-allowlisted, so the guard is exercised). No OAuth is configured — the
    /// fake handler serves the MCP JSON-RPC directly.
    /// </summary>
    public async Task<IMcpToolSession> ConnectWithHttpClientAsync(
        ProviderMcpKey provider,
        string? mcpUrl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var endpoint = ProviderMcpEndpointGuard.EnsureAllowedMcpUrl(provider, mcpUrl);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
        };

        var transport = new HttpClientTransport(transportOptions, httpClient, loggerFactory, ownsHttpClient: false);
        return await ConnectCoreAsync(transport, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IMcpToolSession> ConnectCoreAsync(IClientTransport transport, CancellationToken cancellationToken)
    {
        var client = await McpClient
            .CreateAsync(transport, ClientOptions, loggerFactory, cancellationToken)
            .ConfigureAwait(false);
        return new McpToolSession(client, loggerFactory.CreateLogger<McpToolSession>());
    }
}
