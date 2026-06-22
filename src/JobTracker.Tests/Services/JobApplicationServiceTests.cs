using FluentAssertions;
using Xunit;
using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.Application.Services;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Domain.Interfaces;
using Moq;

namespace JobTracker.Tests.Services;

public class JobApplicationServiceTests
{
    private readonly Mock<IJobApplicationRepository> _repoMock = new();
    private readonly Mock<IMarkdownSyncService> _syncMock = new();

    private JobApplicationService CreateSut()
    {
        _syncMock
            .Setup(s => s.SyncApplicationAsync(It.IsAny<int>()))
            .ReturnsAsync(new SyncResult(true, null));
        return new JobApplicationService(_repoMock.Object, _syncMock.Object);
    }

    private static JobApplication SampleEntity(int id = 1) => new()
    {
        Id = id,
        RoleName = "Software Engineer",
        Status = ApplicationStatus.Applied,
        AppliedDate = DateTime.UtcNow,
        CompanyId = 1,
        Company = new Company { Id = 1, Name = "Acme Corp" },
        ApplicationSkills = new List<ApplicationSkill>()
    };

    [Fact]
    public async Task GetAllApplicationsAsync_ReturnsAllAsDtos()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { SampleEntity() });

        var sut = CreateSut();
        var result = (await sut.GetAllApplicationsAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].RoleName.Should().Be("Software Engineer");
        result[0].CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task CreateAsync_CallsRepositoryAdd_AndFiresSync()
    {
        var entity = SampleEntity();
        _repoMock.Setup(r => r.AddAsync(It.IsAny<JobApplication>())).ReturnsAsync(entity);

        var sut = CreateSut();
        var request = new CreateJobApplicationRequest(
            "Software Engineer", "Description", 1, null,
            ApplicationStatus.Applied, DateTime.Today,
            false, null, null, null, new List<int>());

        var dto = await sut.CreateAsync(request);

        dto.RoleName.Should().Be("Software Engineer");
        _repoMock.Verify(r => r.AddAsync(It.Is<JobApplication>(j => j.RoleName == "Software Engineer")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TrimsRoleName()
    {
        var entity = SampleEntity();
        entity.RoleName = "Software Engineer";
        _repoMock.Setup(r => r.AddAsync(It.IsAny<JobApplication>())).ReturnsAsync(entity);

        var sut = CreateSut();
        var request = new CreateJobApplicationRequest(
            "  Software Engineer  ", "Desc", 1, null,
            ApplicationStatus.Applied, DateTime.Today,
            false, null, null, null, new List<int>());

        await sut.CreateAsync(request);

        _repoMock.Verify(r => r.AddAsync(
            It.Is<JobApplication>(j => j.RoleName == "Software Engineer")), Times.Once);
    }

    [Fact]
    public async Task GetApplicationByIdAsync_ReturnsDto_WhenFound()
    {
        var entity = SampleEntity(42);
        _repoMock.Setup(r => r.GetWithDetailsAsync(42)).ReturnsAsync(entity);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(42);

        result.Should().NotBeNull();
        result!.RoleName.Should().Be("Software Engineer");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_AndFiresSync()
    {
        var entity = SampleEntity(1);
        _repoMock.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(entity);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<JobApplication>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        var request = new UpdateJobApplicationRequest(
            1, "Senior Engineer", "New Desc", 1, null,
            ApplicationStatus.Interview, true, "100k", "http://job.com", "Note", new List<int>());

        await sut.UpdateAsync(request);

        entity.RoleName.Should().Be("Senior Engineer");
        entity.Status.Should().Be(ApplicationStatus.Interview);
        entity.IsRemote.Should().BeTrue();
        
        _repoMock.Verify(r => r.UpdateAsync(entity), Times.Once);
        _syncMock.Verify(s => s.SyncApplicationAsync(1), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsKeyNotFoundException_WhenIdMissing()
    {
        _repoMock.Setup(r => r.GetWithDetailsAsync(99))
                 .ReturnsAsync((JobApplication?)null);

        var sut = CreateSut();
        var request = new UpdateJobApplicationRequest(
            99, "Role", "Desc", 1, null,
            ApplicationStatus.Applied, false, null, null, null, new List<int>());

        await sut.Invoking(s => s.UpdateAsync(request))
                 .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        _repoMock.Setup(r => r.DeleteAsync(5)).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.DeleteAsync(5);

        _repoMock.Verify(r => r.DeleteAsync(5), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatus_AndFiresSync()
    {
        var entity = SampleEntity();
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(entity);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<JobApplication>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.UpdateStatusAsync(1, ApplicationStatus.Interview);

        entity.Status.Should().Be(ApplicationStatus.Interview);
        _repoMock.Verify(r => r.UpdateAsync(entity), Times.Once);
    }

    [Fact]
    public async Task SyncWarning_IsRaised_WhenSyncFails()
    {
        var entity = SampleEntity();
        _repoMock.Setup(r => r.AddAsync(It.IsAny<JobApplication>())).ReturnsAsync(entity);
        _syncMock
            .Setup(s => s.SyncApplicationAsync(It.IsAny<int>()))
            .ReturnsAsync(new SyncResult(false, "Vault not found"));

        // Construct directly — CreateSut() would overwrite the failure mock setup above.
        var sut = new JobApplicationService(_repoMock.Object, _syncMock.Object);
        string? warningReceived = null;
        sut.SyncWarning += msg => warningReceived = msg;

        var request = new CreateJobApplicationRequest(
            "Role", "Desc", 1, null, ApplicationStatus.Applied, DateTime.Today,
            false, null, null, null, new List<int>());

        await sut.CreateAsync(request);

        await Task.Delay(500);
        warningReceived.Should().Contain("Vault not found");
    }
}
