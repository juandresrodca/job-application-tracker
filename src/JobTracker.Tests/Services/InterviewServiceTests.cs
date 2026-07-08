using FluentAssertions;
using Xunit;
using JobTracker.Application.DTOs;
using JobTracker.Application.Services;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Domain.Interfaces;
using Moq;

namespace JobTracker.Tests.Services;

/// <summary>Tests for interview scheduling (Feature: interview calendar).</summary>
public class InterviewServiceTests
{
    private readonly Mock<IInterviewRepository> _repoMock = new();
    private readonly Mock<IJobApplicationRepository> _appRepoMock = new();

    private InterviewService CreateSut() => new(_repoMock.Object, _appRepoMock.Object);

    private static JobApplication SampleApp() => new()
    {
        Id = 7,
        RoleName = "Engineer",
        Company = new Company { Id = 1, Name = "Acme" },
    };

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsDto_WithRoleAndCompany()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Interview>()))
                 .ReturnsAsync((Interview i) => { i.Id = 42; return i; });
        _appRepoMock.Setup(r => r.GetWithDetailsAsync(7)).ReturnsAsync(SampleApp());

        var sut = CreateSut();
        var when = DateTime.Today.AddDays(3).AddHours(14);
        var dto = await sut.CreateAsync(new CreateInterviewRequest(
            7, when, 45, InterviewType.Technical, "  Jane Doe ", "https://meet.example", null));

        dto.Id.Should().Be(42);
        dto.RoleName.Should().Be("Engineer");
        dto.CompanyName.Should().Be("Acme");
        dto.ScheduledAt.Should().Be(when);
        dto.Interviewer.Should().Be("Jane Doe", "whitespace should be trimmed");
        _repoMock.Verify(r => r.AddAsync(It.Is<Interview>(i => i.DurationMinutes == 45)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDateMissing()
    {
        var sut = CreateSut();
        var act = () => sut.CreateAsync(new CreateInterviewRequest(
            7, default, 60, InterviewType.Video, null, null, null));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_DefaultsDurationTo60_WhenNonPositive()
    {
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Interview>()))
                 .ReturnsAsync((Interview i) => i);
        _appRepoMock.Setup(r => r.GetWithDetailsAsync(7)).ReturnsAsync(SampleApp());

        var sut = CreateSut();
        var dto = await sut.CreateAsync(new CreateInterviewRequest(
            7, DateTime.Today.AddDays(1), 0, InterviewType.Phone, null, null, null));

        dto.DurationMinutes.Should().Be(60);
    }

    [Fact]
    public async Task SetCompletedAsync_TogglesFlag()
    {
        var stored = new Interview { Id = 5, ScheduledAt = DateTime.Today, IsCompleted = false };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(stored);

        await CreateSut().SetCompletedAsync(5, true);

        _repoMock.Verify(r => r.UpdateAsync(It.Is<Interview>(i => i.Id == 5 && i.IsCompleted)), Times.Once);
    }

    [Fact]
    public void InterviewDto_WhenText_FormatsTodayAndTomorrow()
    {
        InterviewDto Dto(DateTime at) =>
            new(1, 7, "Engineer", "Acme", at, 60, InterviewType.Video, null, null, null, false);

        Dto(DateTime.Today.AddHours(14.5)).WhenText.Should().StartWith("Today");
        Dto(DateTime.Today.AddDays(1).AddHours(9)).WhenText.Should().StartWith("Tomorrow");
        Dto(DateTime.Today.AddDays(5)).WhenText.Should().NotStartWith("Today");
    }
}
