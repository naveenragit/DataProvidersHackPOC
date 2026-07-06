using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests;

/// <summary>Smoke test proving the test harness builds and runs. Real tests are added per package.</summary>
public sealed class ScaffoldTests
{
    [Fact]
    public void Harness_Runs()
    {
        true.Should().BeTrue();
    }
}
