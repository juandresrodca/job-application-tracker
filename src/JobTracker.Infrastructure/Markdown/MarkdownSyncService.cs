using JobTracker.Application.Interfaces;
using JobTracker.Domain.Entities;
using JobTracker.Domain.Interfaces;
using SyncResult = JobTracker.Application.Interfaces.SyncResult;

namespace JobTracker.Infrastructure.Markdown;

/// <summary>
/// Syncs job applications to Obsidian-compatible Markdown files.
/// Strategy: safe section-based merge — never overwrites user notes section.
/// </summary>
public class MarkdownSyncService : IMarkdownSyncService
{
    private readonly IJobApplicationRepository _appRepo;
    private readonly ISettingsService _settings;

    private const string NotesMarker = "<!-- USER_NOTES_START -->";
    private const string NotesEndMarker = "<!-- USER_NOTES_END -->";

    public MarkdownSyncService(IJobApplicationRepository appRepo, ISettingsService settings)
    {
        _appRepo = appRepo;
        _settings = settings;
    }

    public string? VaultPath
    {
        get => _settings.ObsidianVaultPath;
        set => _settings.ObsidianVaultPath = value;
    }

    public async Task<SyncResult> SyncApplicationAsync(int applicationId)
    {
        if (string.IsNullOrWhiteSpace(VaultPath))
            return new SyncResult(false, "Obsidian vault path is not configured.");
        if (!Directory.Exists(VaultPath))
            return new SyncResult(false, $"Vault folder not found: {VaultPath}");

        try
        {
            var app = await _appRepo.GetWithDetailsAsync(applicationId);
            if (app is null) return new SyncResult(false, $"Application {applicationId} not found.");

            await WriteMarkdownAsync(app);
            return new SyncResult(true, null);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, ex.Message);
        }
    }

    public async Task SyncAllAsync()
    {
        if (string.IsNullOrWhiteSpace(VaultPath) || !Directory.Exists(VaultPath))
            return;

        var apps = await _appRepo.GetAllAsync();
        foreach (var app in apps)
        {
            var full = await _appRepo.GetWithDetailsAsync(app.Id);
            if (full is not null)
                await WriteMarkdownAsync(full);
        }
    }

    public async Task<SyncResult> DeleteApplicationFileAsync(string markdownFileName)
    {
        if (string.IsNullOrWhiteSpace(VaultPath))
            return new SyncResult(false, "Obsidian vault path is not configured.");
        if (!Directory.Exists(VaultPath))
            return new SyncResult(false, $"Vault folder not found: {VaultPath}");

        try
        {
            var filePath = Path.Combine(VaultPath!, markdownFileName);
            if (!File.Exists(filePath))
                return new SyncResult(true, null); // File already doesn't exist, consider it success

            File.Delete(filePath);
            return new SyncResult(true, null);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, ex.Message);
        }
    }

    public async Task<SyncResult> SyncAndCleanupAsync()
    {
        if (string.IsNullOrWhiteSpace(VaultPath))
            return new SyncResult(false, "Obsidian vault path is not configured.");
        if (!Directory.Exists(VaultPath))
            return new SyncResult(false, $"Vault folder not found: {VaultPath}");

        try
        {
            // Step 1: Sync all current applications to markdown files
            var apps = await _appRepo.GetAllAsync();
            var appMarkdownFileNames = new HashSet<string>();

            foreach (var app in apps)
            {
                var full = await _appRepo.GetWithDetailsAsync(app.Id);
                if (full is not null)
                {
                    await WriteMarkdownAsync(full);
                    appMarkdownFileNames.Add(full.MarkdownFileName);
                }
            }

            // Step 2: Find and delete orphaned markdown files (files that don't correspond to any application)
            var markdownFiles = Directory.GetFiles(VaultPath, "*.md");
            var orphanedFiles = new List<string>();

            foreach (var filePath in markdownFiles)
            {
                var fileName = Path.GetFileName(filePath);
                if (!appMarkdownFileNames.Contains(fileName))
                {
                    orphanedFiles.Add(fileName);
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue deleting other orphaned files
                        System.Diagnostics.Debug.WriteLine($"Error deleting orphaned file {fileName}: {ex.Message}");
                    }
                }
            }

            var message = $"Synced {apps.Count()} applications. Deleted {orphanedFiles.Count} orphaned files.";
            return new SyncResult(true, message);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, $"Sync and cleanup error: {ex.Message}");
        }
    }

    private async Task WriteMarkdownAsync(JobApplication app)
    {
        var filePath = Path.Combine(VaultPath!, app.MarkdownFileName);
        var existingUserNotes = ExtractUserNotes(filePath);

        var content = BuildMarkdown(app, existingUserNotes);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static string BuildMarkdown(JobApplication app, string preservedNotes)
    {
        var skills = app.ApplicationSkills.Any()
            ? string.Join("\n", app.ApplicationSkills.Select(s => $"- {s.Skill?.Name}{(s.IsOwned ? " ✅" : "")}"))
            : "- (no skills tagged)";

        return $"""
        ---
        tags: [job-application, {app.Status.ToString().ToLower()}, {app.AppliedDate.Year}]
        status: {app.Status}
        company: "{app.Company?.Name ?? "Unknown"}"
        role: "{app.RoleName}"
        applied_date: {app.AppliedDate:yyyy-MM-dd}
        last_updated: {app.LastUpdated?.ToString("yyyy-MM-dd") ?? app.AppliedDate.ToString("yyyy-MM-dd")}
        contact: "{app.Contact?.Name ?? ""}"
        remote: {(app.IsRemote ? "true" : "false")}
        ---

        # Job Application — {app.RoleName}

        ## Company
        **{app.Company?.Name ?? "Unknown"}**
        {(app.Company?.Website is not null ? $"🌐 [{app.Company.Website}]({app.Company.Website})" : "")}
        {(app.Company?.Location is not null ? $"📍 {app.Company.Location}" : "")}

        ## Status
        > **{app.Status}**

        ## Timeline
        | Field              | Value                        |
        |--------------------|------------------------------|
        | Applied Date       | {app.AppliedDate:yyyy-MM-dd} |
        | Days Since Applied | {app.DaysSinceApplication}   |
        | Last Updated       | {app.LastUpdated?.ToString("yyyy-MM-dd") ?? "-"} |
        | Remote             | {(app.IsRemote ? "Yes" : "No")} |
        | Salary Range       | {app.SalaryRange ?? "-"} |

        ## Contact
        {(app.Contact is not null
            ? $"""
        - **Name:** {app.Contact.Name}
        - **Role:** {app.Contact.Role ?? "-"}
        - **LinkedIn:** {(app.Contact.LinkedInUrl is not null ? $"[Profile]({app.Contact.LinkedInUrl})" : "-")}
        - **Email:** {app.Contact.Email ?? "-"}
        """
            : "_No contact linked._")}

        ## Skills Required
        {skills}

        ## Job Description
        {(string.IsNullOrWhiteSpace(app.JobDescription) ? "_Not captured._" : app.JobDescription)}

        ## Auto Notes
        > ⚡ This section is auto-generated. Do not edit below the USER_NOTES section.
        {(app.JobPostingUrl is not null ? $"- **Job Posting:** [{app.JobPostingUrl}]({app.JobPostingUrl})" : "")}

        ## User Notes
        {NotesMarker}
        {preservedNotes}
        {NotesEndMarker}
        """;
    }

    /// <summary>
    /// Extracts the user-notes section from an existing file so we don't overwrite it on sync.
    /// </summary>
    private static string ExtractUserNotes(string filePath)
    {
        if (!File.Exists(filePath)) return "_Add your personal notes here._";

        var content = File.ReadAllText(filePath);
        var start = content.IndexOf(NotesMarker, StringComparison.Ordinal);
        var end = content.IndexOf(NotesEndMarker, StringComparison.Ordinal);

        if (start < 0 || end < 0 || end <= start) return "_Add your personal notes here._";

        return content[(start + NotesMarker.Length)..end].Trim();
    }
}
