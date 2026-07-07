using JobTracker.Domain.Enums;

namespace JobTracker.Domain.Entities;

public class JobApplication
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;
    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdated { get; set; }
    public string? JobPostingUrl { get; set; }
    public string? SalaryRange { get; set; }
    public bool IsRemote { get; set; }
    public string? Notes { get; set; }

    // FK relationships
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    // Navigation
    public ICollection<ApplicationSkill> ApplicationSkills { get; set; } = new List<ApplicationSkill>();

    // Computed. AppliedDate is entered as a local calendar date (DatePicker), so compare
    // against local Today — UtcNow could be off by one around midnight.
    public int DaysSinceApplication => (DateTime.Today - AppliedDate.Date).Days;

    public string MarkdownFileName =>
        $"{AppliedDate:yyyy-MM-dd}_{SanitizeFileName(Company?.Name ?? "Unknown")}_{SanitizeFileName(RoleName)}_{Id}.md";

    /// <summary>
    /// Filename used before the Id suffix was added (pre-beta). Two applications for the
    /// same role/company/date collided under this scheme — kept only so sync can migrate
    /// previously written files.
    /// </summary>
    public string LegacyMarkdownFileName =>
        $"{AppliedDate:yyyy-MM-dd}_{SanitizeFileName(Company?.Name ?? "Unknown")}_{SanitizeFileName(RoleName)}.md";

    private static string SanitizeFileName(string input) =>
        string.Concat(input.Split(Path.GetInvalidFileNameChars()))
              .Replace(" ", "_")
              .ToLowerInvariant();
}
