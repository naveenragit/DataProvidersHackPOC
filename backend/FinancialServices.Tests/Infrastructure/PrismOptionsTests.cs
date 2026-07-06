using FinancialServices.Api.Infrastructure;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Infrastructure;

/// <summary>
/// Options shape after the point-of-use validation move (fix 8): secrets are no longer boot-gated, so
/// the app binds without a FRED key (health + synthetic-only runs boot). FredApiKey is optional,
/// SecUserAgent carries a default, and the pending provider-API placeholders stay optional. The
/// fail-loud-at-use behaviour is proven in the FredClient/EdgarClient tests.
/// </summary>
public sealed class PrismOptionsTests
{
    [Fact]
    public void FredApiKey_DefaultsToNull_AndIsOptional()
    {
        new PrismOptions().FredApiKey.Should().BeNull();
    }

    [Fact]
    public void SecUserAgent_HasNonBlankDefault()
    {
        new PrismOptions().SecUserAgent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ProviderApis_StayOptional()
    {
        var options = new PrismOptions();

        options.ProviderApis.MoodysApi.Should().BeNull();
        options.ProviderApis.MorningstarApi.Should().BeNull();
    }
}
