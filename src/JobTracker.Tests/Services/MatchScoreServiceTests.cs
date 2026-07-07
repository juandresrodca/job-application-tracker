using FluentAssertions;
using Xunit;
using JobTracker.Application.Interfaces;
using JobTracker.Application.Services;

namespace JobTracker.Tests.Services;

/// <summary>Tests for the offline job-description ↔ skills match score (Feature: CV match).</summary>
public class MatchScoreServiceTests
{
    private readonly MatchScoreService _sut = new();

    private static SkillMatchInput Skill(string name, bool selected = false) => new(name, selected);

    [Fact]
    public void Compute_ScoresSelectedSkills_AgainstMentionedOnes()
    {
        var jd = "We need Azure and PowerShell experience. Docker is a plus.";
        var skills = new[]
        {
            Skill("Azure", selected: true),
            Skill("PowerShell", selected: true),
            Skill("Docker", selected: false),
            Skill("Kubernetes", selected: false), // not mentioned → ignored
        };

        var result = _sut.Compute(jd, skills);

        result.HasDetections.Should().BeTrue();
        result.ScorePercent.Should().Be(66); // 2 of 3 mentioned
        result.MatchedSkills.Should().BeEquivalentTo("Azure", "PowerShell");
        result.MissingSkills.Should().BeEquivalentTo("Docker");
    }

    [Fact]
    public void Compute_UsesWordBoundaries_GitDoesNotMatchDigital()
    {
        var jd = "Join our digital transformation team.";

        var result = _sut.Compute(jd, new[] { Skill("Git", selected: true) });

        result.HasDetections.Should().BeFalse();
    }

    [Theory]
    [InlineData("Experience with CI/CD pipelines required.", "CI/CD")]
    [InlineData("Administration of M365 tenants.", "M365")]
    [InlineData("Defender XDR and SIEM operations.", "Defender XDR")]
    public void Compute_MatchesSkillsWithSpecialCharacters(string jd, string skillName)
    {
        var result = _sut.Compute(jd, new[] { Skill(skillName, selected: true) });

        result.MatchedSkills.Should().Contain(skillName);
        result.ScorePercent.Should().Be(100);
    }

    [Fact]
    public void Compute_IsCaseInsensitive()
    {
        var result = _sut.Compute("looking for AZURE admins", new[] { Skill("Azure", true) });

        result.MatchedSkills.Should().Contain("Azure");
    }

    [Fact]
    public void Compute_ReturnsEmpty_ForBlankDescription()
    {
        _sut.Compute("", new[] { Skill("Azure", true) }).HasDetections.Should().BeFalse();
        _sut.Compute(null, new[] { Skill("Azure", true) }).HasDetections.Should().BeFalse();
    }

    [Fact]
    public void Compute_ReturnsEmpty_WhenNoKnownSkillMentioned()
    {
        var result = _sut.Compute("We sell furniture.", new[] { Skill("Azure"), Skill("SQL") });

        result.HasDetections.Should().BeFalse();
        result.ScorePercent.Should().Be(0);
    }
}
