using JobTracker.Application.DTOs;

namespace JobTracker.Application.Interfaces;

public record SyncResult(bool Success, string? ErrorMessage);

public interface IJobApplicationService
{
    Task<IEnumerable<JobApplicationDto>> GetCurrentWeekApplicationsAsync();
    Task<IEnumerable<JobApplicationDto>> GetAllApplicationsAsync();
    Task<JobApplicationDto?> GetByIdAsync(int id);
    Task<JobApplicationDto> CreateAsync(CreateJobApplicationRequest request);
    Task UpdateAsync(UpdateJobApplicationRequest request);
    Task DeleteAsync(int id);
    Task UpdateStatusAsync(int id, Domain.Enums.ApplicationStatus status);

    /// <summary>Raised on background thread when markdown sync silently fails.</summary>
    event Action<string>? SyncWarning;
}

public interface IMarkdownSyncService
{
    Task<SyncResult> SyncApplicationAsync(int applicationId);
    Task SyncAllAsync();
    Task<SyncResult> DeleteApplicationFileAsync(string markdownFileName);
    Task<SyncResult> SyncAndCleanupAsync();
    string? VaultPath { get; set; }
}

public interface ISettingsService
{
    string? ObsidianVaultPath { get; set; }
    void Save();
    void Load();
}

public interface IPdfExtractionService
{
    /// <summary>Extracts all text from a PDF file at the given path.</summary>
    Task<string> ExtractTextAsync(string filePath);

    /// <summary>Attempts to extract the company name from PDF text.</summary>
    Task<string?> ExtractCompanyNameAsync(string filePath);

    /// <summary>Attempts to extract the job role/title from PDF text.</summary>
    Task<string?> ExtractRoleNameAsync(string filePath);
}

public record EmailExtractionResult(
    string? RoleName,
    string? CompanyName,
    DateTime? AppliedDate,
    string? JobPostingUrl,
    string? Body
);

public interface IEmailExtractionService
{
    /// <summary>Parses raw email text (paste from any client) and extracts job application fields.</summary>
    EmailExtractionResult Extract(string rawEmailText);
}

/// <summary>One skill from the catalog + whether the user has it ticked for this application.</summary>
public record SkillMatchInput(string Name, bool Selected);

public record MatchScoreResult(
    int ScorePercent,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills)
{
    public static readonly MatchScoreResult Empty =
        new(0, Array.Empty<string>(), Array.Empty<string>());

    /// <summary>True when the description mentioned at least one known skill.</summary>
    public bool HasDetections => MatchedSkills.Count + MissingSkills.Count > 0;
}

public interface IMatchScoreService
{
    /// <summary>
    /// Scores a job description against the skill catalog: % of mentioned skills the
    /// user has selected, plus which mentioned skills are still missing. Fully offline.
    /// </summary>
    MatchScoreResult Compute(string? jobDescription, IEnumerable<SkillMatchInput> skills);
}

/// <summary>A job listing pulled from a public job board (e.g. Greenhouse).</summary>
public record DiscoveredJobDto(
    long ExternalId,
    string Title,
    string? Location,
    string Url,
    string? DescriptionText,
    DateTime? UpdatedAt);

/// <summary>
/// Opt-in job discovery against a company's public job board. This is the app's only
/// network-facing feature: it runs exclusively when the user explicitly asks for it —
/// there is no polling, no telemetry, and nothing is sent besides the board request.
/// </summary>
public interface IJobDiscoveryService
{
    /// <summary>
    /// Fetches published jobs from a company's public Greenhouse board
    /// (e.g. slug "stripe" → boards-api.greenhouse.io/v1/boards/stripe/jobs).
    /// Throws HttpRequestException when the board doesn't exist or the network fails.
    /// </summary>
    Task<IReadOnlyList<DiscoveredJobDto>> GetJobsAsync(string boardSlug, CancellationToken ct = default);
}
