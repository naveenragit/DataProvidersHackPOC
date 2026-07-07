using FinancialServices.Api.Connectors.Mcp;
using FluentAssertions;
using ModelContextProtocol.Authentication;
using Xunit;

namespace FinancialServices.Tests.Connectors.Mcp;

/// <summary>
/// Proves the pkg-13 <see cref="TokenFreshness"/> refresh/skew math (pure, clock-injected — no network):
/// a token inside the skew window, expired, blank, or with an unknown lifetime is "refresh due"; a fresh
/// one is not. This is the offline-testable half of the OAuth broker (the SDK owns the real HTTP refresh).
/// </summary>
public sealed class TokenFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);

    private static TokenContainer Token(string? access, int? expiresIn, DateTimeOffset obtainedAt) => new()
    {
        TokenType = "Bearer",
        AccessToken = access!,
        RefreshToken = "refresh",
        ExpiresIn = expiresIn,
        Scope = "offline_access",
        ObtainedAt = obtainedAt,
    };

    [Fact]
    public void SecondsUntilExpiry_is_null_when_no_expiry_provided()
    {
        TokenFreshness.SecondsUntilExpiry(Token("a", null, Now), Now).Should().BeNull();
    }

    [Fact]
    public void SecondsUntilExpiry_counts_down_from_obtained_plus_expiresIn()
    {
        TokenFreshness.SecondsUntilExpiry(Token("a", 3600, Now), Now.AddSeconds(100))
            .Should().BeApproximately(3500, 0.001);
    }

    [Fact]
    public void NeedsRefresh_is_false_for_a_fresh_token()
    {
        TokenFreshness.NeedsRefresh(Token("a", 3600, Now), Now).Should().BeFalse();
    }

    [Fact]
    public void NeedsRefresh_is_true_inside_the_default_skew_window()
    {
        // 30s of life left, default skew is 60s → due.
        TokenFreshness.NeedsRefresh(Token("a", 3600, Now), Now.AddSeconds(3570)).Should().BeTrue();
    }

    [Fact]
    public void NeedsRefresh_is_true_when_already_expired()
    {
        TokenFreshness.NeedsRefresh(Token("a", 60, Now), Now.AddSeconds(120)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NeedsRefresh_is_true_for_a_blank_access_token(string? access)
    {
        TokenFreshness.NeedsRefresh(Token(access, 3600, Now), Now).Should().BeTrue();
    }

    [Fact]
    public void NeedsRefresh_is_true_when_the_lifetime_is_unknown()
    {
        TokenFreshness.NeedsRefresh(Token("a", null, Now), Now).Should().BeTrue();
    }
}
