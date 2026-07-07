using FluentAssertions;
using Xunit;
using System.IO;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Enums;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Markdown;
using Moq;

namespace JobTracker.Tests.Services;

/// <summary>
/// Regression tests for the vault-safety rules:
///  - the app writes only inside the "JobTracker" subfolder,
///  - cleanup NEVER touches the vault root (the user's own notes),
///  - cleanup only deletes files carrying the app's USER_NOTES marker,
///  - user notes survive re-sync and legacy files are migrated,
///  - filenames are collision-free (Id suffix).
/// </summary>
public class MarkdownSyncServiceTests : IDisposable
{
    private const string Marker = "<!-- USER_NOTES_START -->";

    private readonly string _vault = Path.Combine(Path.GetTempPath(), "JobTrackerVaultTests_" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IJobApplicationRepository> _repoMock = new();
    private readonly Mock<ISettingsService> _settingsMock = new();

    private string SyncFolder => Path.Combine(_vault, "JobTracker");

    public MarkdownSyncServiceTests()
    {
        Directory.CreateDirectory(_vault);
        _settingsMock.SetupProperty(s => s.ObsidianVaultPath, _vault);
    }

    public void Dispose()
    {
        if (Directory.Exists(_vault))
            Directory.Delete(_vault, true);
    }

    private MarkdownSyncService CreateSut() => new(_repoMock.Object, _settingsMock.Object);

    private static JobApplication SampleApp(int id = 1, string role = "Engineer", string company = "Acme Corp") => new()
    {
        Id = id,
        RoleName = role,
        Status = ApplicationStatus.Applied,
        AppliedDate = new DateTime(2026, 1, 15),
        CompanyId = 1,
        Company = new Company { Id = 1, Name = company },
        ApplicationSkills = new List<ApplicationSkill>()
    };

    [Fact]
    public async Task SyncAndCleanup_NeverDeletesUserFiles_InVaultRoot()
    {
        // The user's personal Obsidian notes live in the vault root
        var userNote = Path.Combine(_vault, "My Personal Note.md");
        File.WriteAllText(userNote, "# my private stuff");

        // Even a stale .md in the root that we don't recognize must survive
        var strayNote = Path.Combine(_vault, "2020-01-01_old_company_old_role.md");
        File.WriteAllText(strayNote, "# some other tool's file");

        var app = SampleApp();
        _repoMock.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new[] { app });

        var result = await CreateSut().SyncAndCleanupAsync();

        result.Success.Should().BeTrue();
        File.Exists(userNote).Should().BeTrue("cleanup must never touch the vault root");
        File.Exists(strayNote).Should().BeTrue("cleanup must never touch the vault root");
        File.Exists(Path.Combine(SyncFolder, app.MarkdownFileName))
            .Should().BeTrue("app files belong in the JobTracker subfolder");
    }

    [Fact]
    public async Task SyncAndCleanup_DeletesOnlyAppOwnedOrphans_InsideSyncFolder()
    {
        Directory.CreateDirectory(SyncFolder);

        // Orphan written by the app (has our marker) → should be removed
        var appOrphan = Path.Combine(SyncFolder, "2020-01-01_gone_company_gone_role_99.md");
        File.WriteAllText(appOrphan, $"# old app file\n{Marker}\nnotes\n<!-- USER_NOTES_END -->");

        // File the user dropped into our folder (no marker) → must survive
        var userFileInFolder = Path.Combine(SyncFolder, "user-dropped-file.md");
        File.WriteAllText(userFileInFolder, "# user file without marker");

        _repoMock.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(new[] { SampleApp() });

        var result = await CreateSut().SyncAndCleanupAsync();

        result.Success.Should().BeTrue();
        File.Exists(appOrphan).Should().BeFalse("orphaned app-owned files should be cleaned up");
        File.Exists(userFileInFolder).Should().BeTrue("files without our marker are not ours to delete");
    }

    [Fact]
    public async Task Sync_PreservesUserNotes_OnResync()
    {
        var app = SampleApp();
        _repoMock.Setup(r => r.GetWithDetailsAsync(app.Id)).ReturnsAsync(app);

        var sut = CreateSut();
        (await sut.SyncApplicationAsync(app.Id)).Success.Should().BeTrue();

        // User edits their notes section
        var filePath = Path.Combine(SyncFolder, app.MarkdownFileName);
        var content = File.ReadAllText(filePath);
        content = content.Replace("_Add your personal notes here._", "Interview went well — follow up Friday!");
        File.WriteAllText(filePath, content);

        (await sut.SyncApplicationAsync(app.Id)).Success.Should().BeTrue();

        File.ReadAllText(filePath).Should().Contain("Interview went well — follow up Friday!");
    }

    [Fact]
    public async Task Sync_MigratesLegacyRootFile_AndKeepsItsNotes()
    {
        var app = SampleApp();
        _repoMock.Setup(r => r.GetWithDetailsAsync(app.Id)).ReturnsAsync(app);

        // Pre-beta versions wrote to the vault root without the Id suffix
        var legacyPath = Path.Combine(_vault, app.LegacyMarkdownFileName);
        File.WriteAllText(legacyPath,
            $"# old file\n{Marker}\nnotes written long ago\n<!-- USER_NOTES_END -->");

        (await CreateSut().SyncApplicationAsync(app.Id)).Success.Should().BeTrue();

        var newPath = Path.Combine(SyncFolder, app.MarkdownFileName);
        File.Exists(newPath).Should().BeTrue();
        File.ReadAllText(newPath).Should().Contain("notes written long ago");
        File.Exists(legacyPath).Should().BeFalse("app-owned legacy file should be migrated away from the root");
    }

    [Fact]
    public void MarkdownFileName_IsUnique_ForSameRoleCompanyAndDate()
    {
        var first  = SampleApp(id: 1);
        var second = SampleApp(id: 2);

        first.MarkdownFileName.Should().NotBe(second.MarkdownFileName,
            "two applications for the same role/company/date must not overwrite each other");
    }
}
