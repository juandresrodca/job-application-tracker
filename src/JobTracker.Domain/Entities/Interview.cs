using JobTracker.Domain.Enums;

namespace JobTracker.Domain.Entities;

/// <summary>A scheduled interview belonging to a job application.</summary>
public class Interview
{
    public int Id { get; set; }
    public int JobApplicationId { get; set; }
    public JobApplication? JobApplication { get; set; }

    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public InterviewType Type { get; set; } = InterviewType.Video;

    public string? Interviewer { get; set; }
    /// <summary>Meeting link for remote interviews, address for on-site ones.</summary>
    public string? LocationOrLink { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }

    // Computed
    public bool IsUpcoming => !IsCompleted && ScheduledAt >= DateTime.Now;
    public bool IsToday => ScheduledAt.Date == DateTime.Today;
    public int DaysUntil => (ScheduledAt.Date - DateTime.Today).Days;
}
