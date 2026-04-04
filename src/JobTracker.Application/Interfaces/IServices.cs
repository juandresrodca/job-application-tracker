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
