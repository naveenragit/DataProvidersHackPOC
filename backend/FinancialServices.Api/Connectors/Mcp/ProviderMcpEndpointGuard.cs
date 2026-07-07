using System.Net;
using FinancialServices.Api.Infrastructure.Errors;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>Which live MCP rating provider a call targets (maps 1:1 to <c>PrismOptions.Providers.*</c>).</summary>
public enum ProviderMcpKey
{
    /// <summary>Morningstar/DBRS hosted MCP server.</summary>
    Morningstar,

    /// <summary>Moody's hosted MCP server.</summary>
    Moodys,
}

/// <summary>
/// SSRF guard for the live-provider MCP + OAuth URLs (SEC-03), mirroring the
/// <see cref="EdgarClient"/> <c>EnsureAllowedHost</c> pattern but generalized per provider. Every
/// externally-reachable URL Prism connects to is validated <b>before</b> a socket is opened:
/// <list type="bullet">
/// <item>the configured MCP endpoint must be <c>https</c> on an <b>exact-host allowlist</b>
/// (Morningstar → <c>mcp.morningstar.com</c>, Moody's → <c>api.moodys.com</c>) — never an IP
/// literal, loopback, or link-local address;</item>
/// <item>the OAuth discovery/authorization/token servers the SDK follows (RFC 8414/9728) are bounded
/// to the provider's own registrable domain (wired via <c>ClientOAuthOptions.AuthServerSelector</c>);</item>
/// <item>the OAuth redirect must be a <b>loopback</b> <c>http</c> URI — the single, deliberate
/// loopback exception (the native-app authorization-code flow requires it).</item>
/// </list>
/// A rejection is a configuration fault, so it fails <b>loud</b> with
/// <see cref="ConfigurationException"/> naming the setting (P1) — never a silent pass.
/// </summary>
public static class ProviderMcpEndpointGuard
{
    private sealed record ProviderHosts(string ExactMcpHost, string RegistrableDomain);

    // Exact MCP host + the registrable domain the OAuth servers must stay within. Ordinal, lowercase.
    private static readonly IReadOnlyDictionary<ProviderMcpKey, ProviderHosts> Allowed =
        new Dictionary<ProviderMcpKey, ProviderHosts>
        {
            [ProviderMcpKey.Morningstar] = new("mcp.morningstar.com", "morningstar.com"),
            [ProviderMcpKey.Moodys] = new("api.moodys.com", "moodys.com"),
        };

    /// <summary>The config key prefix for a provider (for fail-loud messages).</summary>
    public static string SettingPrefix(ProviderMcpKey provider) => $"Prism:Providers:{provider}";

    /// <summary>
    /// Validates the configured MCP endpoint and returns the parsed <see cref="Uri"/>. Throws
    /// <see cref="ConfigurationException"/> when unset, not absolute, not <c>https</c>, an IP/loopback/
    /// link-local host, or not the provider's allow-listed host.
    /// </summary>
    public static Uri EnsureAllowedMcpUrl(ProviderMcpKey provider, string? mcpUrl)
    {
        var setting = $"{SettingPrefix(provider)}:McpUrl";
        if (string.IsNullOrWhiteSpace(mcpUrl))
        {
            throw new ConfigurationException(setting, $"{provider} MCP URL is not configured.");
        }

        if (!Uri.TryCreate(mcpUrl, UriKind.Absolute, out var uri))
        {
            throw new ConfigurationException(setting, $"{provider} MCP URL '{mcpUrl}' is not an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(setting, $"{provider} MCP URL must use https; got '{uri.Scheme}'.");
        }

        if (IsIpLoopbackOrLinkLocal(uri))
        {
            throw new ConfigurationException(
                setting, $"{provider} MCP URL must be a public hostname, not an IP/loopback/link-local address.");
        }

        var expected = Allowed[provider].ExactMcpHost;
        if (!string.Equals(uri.Host, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(
                setting, $"{provider} MCP URL host '{uri.Host}' is not the allow-listed host '{expected}'.");
        }

        return uri;
    }

    /// <summary>
    /// Validates the OAuth redirect URI and returns the parsed <see cref="Uri"/>. The redirect MUST be
    /// a loopback <c>http</c> address (the native-app flow's callback listener) — the one loopback
    /// exception to the SSRF policy. Throws <see cref="ConfigurationException"/> otherwise.
    /// </summary>
    public static Uri EnsureLoopbackRedirect(ProviderMcpKey provider, string? redirectUri)
    {
        var setting = $"{SettingPrefix(provider)}:RedirectUri";
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ConfigurationException(setting, $"{provider} OAuth redirect URI is not configured.");
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            throw new ConfigurationException(
                setting, $"{provider} OAuth redirect URI '{redirectUri}' is not an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(
                setting, $"{provider} OAuth redirect URI must be loopback http; got scheme '{uri.Scheme}'.");
        }

        if (!IsLoopbackHost(uri))
        {
            throw new ConfigurationException(
                setting, $"{provider} OAuth redirect URI host '{uri.Host}' must be a loopback address (localhost/127.0.0.1/[::1]).");
        }

        return uri;
    }

    /// <summary>
    /// Whether a discovered OAuth authorization/token server URL is acceptable for
    /// <paramref name="provider"/> — <c>https</c>, not IP/loopback/link-local, and within the
    /// provider's own registrable domain. Wired into <c>ClientOAuthOptions.AuthServerSelector</c> so
    /// the SDK cannot be steered to an attacker-advertised server (SEC-03, discovery vector).
    /// </summary>
    public static bool IsAllowedAuthServer(ProviderMcpKey provider, Uri authServer)
    {
        ArgumentNullException.ThrowIfNull(authServer);

        if (!string.Equals(authServer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            IsIpLoopbackOrLinkLocal(authServer))
        {
            return false;
        }

        var domain = Allowed[provider].RegistrableDomain;
        return string.Equals(authServer.Host, domain, StringComparison.OrdinalIgnoreCase) ||
               authServer.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopbackHost(Uri uri)
    {
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
            IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    // Reject any IP literal (even public — an MCP host is always a DNS name), loopback, and link-local
    // (169.254.0.0/16, fe80::/10) so a rebinding/metadata SSRF cannot slip through the allowlist.
    private static bool IsIpLoopbackOrLinkLocal(Uri uri)
    {
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6))
        {
            return false;
        }

        if (!IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip))
        {
            return true; // Looks like an IP but won't parse → reject.
        }

        return IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || IsIPv4LinkLocal(ip);
    }

    private static bool IsIPv4LinkLocal(IPAddress ip)
    {
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }
}
