namespace JobTracker.Domain.Entities;

public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Role { get; set; }
    public string? Notes { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
}
