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

    // Computed
    public int DaysSinceApplication => (DateTime.UtcNow - AppliedDate).Days;

    public string MarkdownFileName =>
        $"{AppliedDate:yyyy-MM-dd}_{SanitizeFileName(Company?.Name ?? "Unknown")}_{SanitizeFileName(RoleName)}.md";

    private static string SanitizeFileName(string input) =>
        string.Concat(input.Split(Path.GetInvalidFileNameChars()))
              .Replace(" ", "_")
              .ToLowerInvariant();
}
