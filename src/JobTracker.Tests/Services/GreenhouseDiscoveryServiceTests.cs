using FluentAssertions;
using Xunit;
using JobTracker.Infrastructure.Discovery;

namespace JobTracker.Tests.Services;

/// <summary>
/// Offline tests for the discovery service's HTML handling (Feature: job discovery).
/// Network behaviour is exercised manually — no live HTTP in unit tests.
/// </summary>
public class GreenhouseDiscoveryServiceTests
{
    [Fact]
    public void HtmlToPlainText_StripsTags_AndDecodesEntities()
    {
        var html = "&lt;p&gt;We need &lt;strong&gt;Azure&lt;/strong&gt; skills&lt;/p&gt;&lt;ul&gt;&lt;li&gt;PowerShell&lt;/li&gt;&lt;/ul&gt;";

        var text = GreenhouseDiscoveryService.HtmlToPlainText(html);

        text.Should().NotBeNull();
        text.Should().Contain("We need Azure skills");
        text.Should().Contain("PowerShell");
        text.Should().NotContain("<");
        text.Should().NotContain("&lt;");
    }

    [Fact]
    public void HtmlToPlainText_ConvertsBlockTags_ToNewlines()
    {
        var text = GreenhouseDiscoveryService.HtmlToPlainText("<p>First</p><p>Second</p>");

        text.Should().Contain("First");
        text.Should().Contain("Second");
        text!.IndexOf("First", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("Second", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HtmlToPlainText_ReturnsNull_ForEmptyInput(string? html)
    {
        GreenhouseDiscoveryService.HtmlToPlainText(html).Should().BeNull();
    }
}
