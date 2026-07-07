using FluentAssertions;
using Xunit;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;

namespace JobTracker.Tests.Services;

/// <summary>Tests for the follow-up nudge rules (Feature: follow-up reminders).</summary>
public class FollowUpLogicTests
{
    private static JobApplication App(ApplicationStatus status, int daysSinceActivity) => new()
    {
        Id = 1,
        RoleName = "Engineer",
        Status = status,
        AppliedDate = DateTime.Today.AddDays(-daysSinceActivity - 10),
        LastUpdated = DateTime.Today.AddDays(-daysSinceActivity),
    };

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Screening)]
    [InlineData(ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.TechnicalTest)]
    public void NeedsFollowUp_IsTrue_ForActiveStatus_After14QuietDays(ApplicationStatus status)
    {
        App(status, 14).NeedsFollowUp.Should().BeTrue();
        App(status, 30).NeedsFollowUp.Should().BeTrue();
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Interview)]
    public void NeedsFollowUp_IsFalse_WhenRecentActivity(ApplicationStatus status)
    {
        App(status, 0).NeedsFollowUp.Should().BeFalse();
        App(status, 13).NeedsFollowUp.Should().BeFalse();
    }

    [Theory]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void NeedsFollowUp_IsFalse_ForClosedOrWonStatuses_EvenWhenStale(ApplicationStatus status)
    {
        App(status, 60).NeedsFollowUp.Should().BeFalse();
    }

    [Fact]
    public void DaysSinceLastActivity_FallsBackToAppliedDate_WhenNeverUpdated()
    {
        var app = new JobApplication
        {
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.Today.AddDays(-20),
            LastUpdated = null,
        };

        app.DaysSinceLastActivity.Should().Be(20);
        app.NeedsFollowUp.Should().BeTrue();
    }
}
