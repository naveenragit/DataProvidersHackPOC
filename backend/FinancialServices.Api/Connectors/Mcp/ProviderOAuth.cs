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
/// <para>
/// <b>Client identification.</b> A hosted MCP server (Morningstar, Moody's) does not hand out a
/// long-lived pre-registered <c>client_id</c>; its well-known advertises a <c>registration_endpoint</c>
/// and expects clients to register on the fly. So a <c>ClientId</c> is <b>optional</b>: when config
/// supplies one we use it; otherwise the SDK performs RFC 7591 <b>dynamic client registration</b> and we
/// persist the issued client via <paramref name="onClientRegistered"/> (its refresh token is bound to
/// that client). Passing a <c>client_id</c> the server does not recognise is what surfaces as
/// "client not found" on the authorize page — leaving it blank lets DCR mint a valid one.
/// </para>
/// </summary>
public static class ProviderOAuth
{
    /// <summary>The single OAuth scope Prism requests — a refresh token for headless reuse (SEC-07).</summary>
    public static readonly IReadOnlyList<string> Scopes = new[] { "offline_access" };

    /// <summary>Human-readable client name sent during dynamic client registration (shown at consent).</summary>
    public const string DynamicClientName = "Prism Rating Reconciler";

    /// <summary>
    /// Assembles <see cref="ClientOAuthOptions"/>. Fails <b>loud</b> (<see cref="ConfigurationException"/>)
    /// on a bad redirect URI (P1). The <paramref name="redirect"/> is interactive (CLI login) or the
    /// headless throwing delegate (<see cref="HeadlessRedirect"/>) at runtime.
    /// <para>
    /// Client resolution order: an explicit config <c>ClientId</c> → a <paramref name="persistedClient"/>
    /// from a prior dynamic registration → otherwise RFC 7591 dynamic client registration (the SDK POSTs
    /// to the discovered <c>registration_endpoint</c>, and <paramref name="onClientRegistered"/> persists
    /// the result for reuse).
    /// </para>
    /// </summary>
    public static ClientOAuthOptions BuildOptions(
        ProviderMcpKey provider,
        PrismOptions.ProviderMcpOptions options,
        ITokenCache tokenCache,
        AuthorizationRedirectDelegate redirect,
        RegisteredClient? persistedClient = null,
        Func<DynamicClientRegistrationResponse, CancellationToken, Task>? onClientRegistered = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenCache);
        ArgumentNullException.ThrowIfNull(redirect);

        var redirectUri = ProviderMcpEndpointGuard.EnsureLoopbackRedirect(provider, options.RedirectUri);

        // Prefer an explicitly configured client; otherwise fall back to a client previously minted by
        // dynamic client registration. A blank secret is a public client — pass null, never an empty string.
        var configuredClientId = string.IsNullOrWhiteSpace(options.ClientId) ? null : options.ClientId;
        var effectiveClientId = configuredClientId ?? persistedClient?.ClientId;
        var effectiveClientSecret = configuredClientId is not null
            ? (string.IsNullOrWhiteSpace(options.ClientSecret) ? null : options.ClientSecret)
            : persistedClient?.ClientSecret;

        var oauthOptions = new ClientOAuthOptions
        {
            ClientId = effectiveClientId,
            ClientSecret = effectiveClientSecret,
            RedirectUri = redirectUri,
            Scopes = Scopes,
            TokenCache = tokenCache,
            AuthorizationRedirectDelegate = redirect,
            AuthServerSelector = GuardedAuthServerSelector(provider),
        };

        // No client_id available → let the SDK register dynamically (RFC 7591) against the provider's
        // advertised registration_endpoint, and persist the issued client so later runs / headless
        // refreshes present the same client the refresh token is bound to.
        if (effectiveClientId is null)
        {
            oauthOptions.DynamicClientRegistration = new DynamicClientRegistrationOptions
            {
                ClientName = DynamicClientName,
                // Some registration endpoints gate /register behind a bearer; supply it only if configured.
                InitialAccessToken = string.IsNullOrWhiteSpace(options.RegistrationAccessToken)
                    ? null
                    : options.RegistrationAccessToken,
                ResponseDelegate = onClientRegistered,
            };
        }

        return oauthOptions;
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
