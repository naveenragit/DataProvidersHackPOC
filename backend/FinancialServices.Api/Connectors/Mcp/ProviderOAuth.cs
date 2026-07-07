using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// Builds the MCP SDK's <see cref="ClientOAuthOptions"/> for a live provider from Prism config, wiring
/// in the SSRF <see cref="ProviderMcpEndpointGuard"/> and the two redirect strategies. Scope is
/// minimized to <c>offline_access</c> only (SEC-07 — no <c>openid</c>/<c>email</c>/<c>profile</c>, so
/// no id_token PII is requested); the refresh token the SDK persists via the supplied
/// <see cref="ITokenCache"/> is what the runtime reuses headlessly.
/// </summary>
public static class ProviderOAuth
{
    /// <summary>The single OAuth scope Prism requests — a refresh token for headless reuse (SEC-07).</summary>
    public static readonly IReadOnlyList<string> Scopes = new[] { "offline_access" };

    /// <summary>
    /// Assembles <see cref="ClientOAuthOptions"/>. Fails <b>loud</b> (<see cref="ConfigurationException"/>)
    /// when the provider is enabled but has no <c>ClientId</c> or a bad redirect URI (P1). The
    /// <paramref name="redirect"/> is interactive (CLI login) or the headless throwing delegate
    /// (<see cref="HeadlessRedirect"/>) at runtime.
    /// </summary>
    public static ClientOAuthOptions BuildOptions(
        ProviderMcpKey provider,
        PrismOptions.ProviderMcpOptions options,
        ITokenCache tokenCache,
        AuthorizationRedirectDelegate redirect)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenCache);
        ArgumentNullException.ThrowIfNull(redirect);

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ConfigurationException(
                $"{ProviderMcpEndpointGuard.SettingPrefix(provider)}:ClientId",
                $"{provider} is enabled but has no OAuth ClientId configured.");
        }

        var redirectUri = ProviderMcpEndpointGuard.EnsureLoopbackRedirect(provider, options.RedirectUri);

        return new ClientOAuthOptions
        {
            ClientId = options.ClientId,
            // A blank secret is a public client (e.g. CIMD) — pass null, never an empty-string secret.
            ClientSecret = string.IsNullOrWhiteSpace(options.ClientSecret) ? null : options.ClientSecret,
            RedirectUri = redirectUri,
            Scopes = Scopes,
            TokenCache = tokenCache,
            AuthorizationRedirectDelegate = redirect,
            AuthServerSelector = GuardedAuthServerSelector(provider),
        };
    }

    /// <summary>
    /// A redirect delegate for headless/runtime contexts: it never opens a browser or blocks on
    /// <c>Console.ReadLine</c> (the SDK default would — STK-03). A missing/expired refresh token
    /// therefore surfaces as a loud <see cref="ReloginRequiredException"/> at the login/ingestion
    /// boundary instead of wedging a request.
    /// </summary>
    public static AuthorizationRedirectDelegate HeadlessRedirect(ProviderMcpKey provider) =>
        (_, _, _) => throw new ReloginRequiredException(
            provider.ToString(),
            $"{provider} requires a one-time interactive login; run the provider-discovery CLI to authenticate.");

    // Bound the OAuth servers the SDK follows during RFC 8414/9728 discovery to the provider's own
    // registrable domain over https (SEC-03). Rejecting all advertised servers fails loud rather than
    // silently connecting to an attacker-advertised authorization server.
    private static Func<IReadOnlyList<Uri>, Uri> GuardedAuthServerSelector(ProviderMcpKey provider) =>
        servers =>
        {
            foreach (var server in servers)
            {
                if (ProviderMcpEndpointGuard.IsAllowedAuthServer(provider, server))
                {
                    return server;
                }
            }

            throw new ConfigurationException(
                $"{ProviderMcpEndpointGuard.SettingPrefix(provider)}:McpUrl",
                $"{provider} advertised no OAuth authorization server within its own domain over https (SSRF guard).");
        };
}
