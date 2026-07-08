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
    string? SalaryRange,
    int DaysSinceLastActivity = 0,
    bool NeedsFollowUp = false,
    // Interview summary — enriched by the dashboard so the right-click menu knows
    // whether an interview already exists and which one to modify.
    int InterviewCount = 0,
    int? NextInterviewId = null,
    DateTime? NextInterviewAt = null
)
{
    public bool HasInterview => InterviewCount > 0;
}

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

public record InterviewDto(
    int Id,
    int JobApplicationId,
    string RoleName,
    string CompanyName,
    DateTime ScheduledAt,
    int DurationMinutes,
    InterviewType Type,
    string? Interviewer,
    string? LocationOrLink,
    string? Notes,
    bool IsCompleted)
{
    public bool IsToday => ScheduledAt.Date == DateTime.Today;
    public bool IsTomorrow => ScheduledAt.Date == DateTime.Today.AddDays(1);
    public int DaysUntil => (ScheduledAt.Date - DateTime.Today).Days;

    /// <summary>"Today 14:30", "Tomorrow 09:00" or "Mon 21 Jul · 14:30".</summary>
    public string WhenText => IsToday
        ? $"Today {ScheduledAt:HH:mm}"
        : IsTomorrow
            ? $"Tomorrow {ScheduledAt:HH:mm}"
            : ScheduledAt.ToString("ddd d MMM · HH:mm");
}

public record CreateInterviewRequest(
    int JobApplicationId,
    DateTime ScheduledAt,
    int DurationMinutes,
    InterviewType Type,
    string? Interviewer,
    string? LocationOrLink,
    string? Notes);

public record UpdateInterviewRequest(
    int Id,
    DateTime ScheduledAt,
    int DurationMinutes,
    InterviewType Type,
    string? Interviewer,
    string? LocationOrLink,
    string? Notes,
    bool IsCompleted);

public record CompanyDto(int Id, string Name, string? Website, string? Industry, string? Location);
public record ContactDto(int Id, string Name, string? Email, string? LinkedInUrl, string? Role, int? CompanyId);
public record SkillDto(int Id, string Name, string? Category);
