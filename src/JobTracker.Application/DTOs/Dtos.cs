using JobTracker.Domain.Enums;

namespace JobTracker.Application.DTOs;

public record JobApplicationDto(
    int Id,
    string RoleName,
    string CompanyName,
    ApplicationStatus Status,
    DateTime AppliedDate,
    int DaysSinceApplication,
    string? ContactName,
    string? ContactLinkedIn,
    IReadOnlyList<string> Skills,
    string? Notes,
    bool IsRemote,
    string? SalaryRange
);

public record CreateJobApplicationRequest(
    string RoleName,
    string JobDescription,
    int CompanyId,
    int? ContactId,
    ApplicationStatus Status,
    DateTime AppliedDate,
    bool IsRemote,
    string? SalaryRange,
    string? Notes,
    string? JobPostingUrl,
    IList<int> SkillIds
);

public record UpdateJobApplicationRequest(
    int Id,
    string RoleName,
    string JobDescription,
    int CompanyId,
    int? ContactId,
    ApplicationStatus Status,
    bool IsRemote,
    string? SalaryRange,
    string? Notes,
    string? JobPostingUrl,
    IList<int> SkillIds
);

public record CompanyDto(int Id, string Name, string? Website, string? Industry, string? Location);
public record ContactDto(int Id, string Name, string? Email, string? LinkedInUrl, string? Role, int? CompanyId);
public record SkillDto(int Id, string Name, string? Category);
