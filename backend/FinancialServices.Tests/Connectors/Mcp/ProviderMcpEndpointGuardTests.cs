using FinancialServices.Api.Connectors.Mcp;
using FinancialServices.Api.Infrastructure.Errors;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Connectors.Mcp;

/// <summary>
/// Proves the pkg-13 SSRF guard (SEC-03): every live-provider MCP/OAuth URL is validated before a socket
/// opens — <c>https</c> + exact-host allowlist for the MCP endpoint, loopback-<c>http</c> for the OAuth
/// redirect, and the provider's own registrable domain for any discovery-advertised auth server. A
/// rejection is a fail-loud <see cref="ConfigurationException"/> (P1), never a silent pass.
/// </summary>
public sealed class ProviderMcpEndpointGuardTests
{
    [Theory]
    [InlineData(ProviderMcpKey.Morningstar, "https://mcp.morningstar.com/mcp")]
    [InlineData(ProviderMcpKey.Moodys, "https://api.moodys.com/genai-ready-data/m1/mcp")]
    public void EnsureAllowedMcpUrl_accepts_the_allow_listed_https_host(ProviderMcpKey provider, string url)
    {
        ProviderMcpEndpointGuard.EnsureAllowedMcpUrl(provider, url).Host.Should().Be(new Uri(url).Host);
    }

    [Theory]
    [InlineData(ProviderMcpKey.Morningstar, null)]                                       // unset
    [InlineData(ProviderMcpKey.Morningstar, "http://mcp.morningstar.com/mcp")]           // not https
    [InlineData(ProviderMcpKey.Morningstar, "https://api.moodys.com/mcp")]               // other provider's host
    [InlineData(ProviderMcpKey.Morningstar, "https://evil.example.com/mcp")]             // off-allowlist
    [InlineData(ProviderMcpKey.Morningstar, "https://mcp.morningstar.com.evil.com/mcp")] // suffix-spoof
    [InlineData(ProviderMcpKey.Morningstar, "https://127.0.0.1/mcp")]                    // loopback IP
    [InlineData(ProviderMcpKey.Morningstar, "https://169.254.169.254/latest/meta-data")]// link-local metadata SSRF
    [InlineData(ProviderMcpKey.Morningstar, "https://localhost/mcp")]                    // loopback name
    [InlineData(ProviderMcpKey.Morningstar, "not-a-uri")]                               // malformed
    public void EnsureAllowedMcpUrl_rejects_everything_off_the_allowlist(ProviderMcpKey provider, string? url)
    {
        var act = () => ProviderMcpEndpointGuard.EnsureAllowedMcpUrl(provider, url);
        act.Should().Throw<ConfigurationException>();
    }

    [Theory]
    [InlineData("http://localhost:8765/callback")]
    [InlineData("http://127.0.0.1:8765/callback")]
    [InlineData("http://[::1]:8765/callback")]
    public void EnsureLoopbackRedirect_accepts_loopback_http(string url)
    {
        ProviderMcpEndpointGuard.EnsureLoopbackRedirect(ProviderMcpKey.Moodys, url).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]                                    // unset
    [InlineData("https://localhost:8765/callback")]       // not http
    [InlineData("http://mcp.morningstar.com/callback")]   // not loopback
    public void EnsureLoopbackRedirect_rejects_non_loopback_or_non_http(string? url)
    {
        var act = () => ProviderMcpEndpointGuard.EnsureLoopbackRedirect(ProviderMcpKey.Moodys, url);
        act.Should().Throw<ConfigurationException>();
    }

    [Theory]
    [InlineData(ProviderMcpKey.Morningstar, "https://sso.morningstar.com/authorize", true)]
    [InlineData(ProviderMcpKey.Morningstar, "https://morningstar.com/token", true)]
    [InlineData(ProviderMcpKey.Moodys, "https://auth.moodys.com/oauth/token", true)]
    [InlineData(ProviderMcpKey.Moodys, "https://evil.com/token", false)]                 // foreign domain
    [InlineData(ProviderMcpKey.Moodys, "https://api.moodys.com.evil.com/token", false)]  // suffix-spoof
    [InlineData(ProviderMcpKey.Moodys, "http://auth.moodys.com/oauth", false)]           // not https
    [InlineData(ProviderMcpKey.Moodys, "https://127.0.0.1/oauth", false)]                // IP
    public void IsAllowedAuthServer_bounds_to_the_provider_registrable_domain(
        ProviderMcpKey provider, string url, bool expected)
    {
        ProviderMcpEndpointGuard.IsAllowedAuthServer(provider, new Uri(url)).Should().Be(expected);
    }
}
