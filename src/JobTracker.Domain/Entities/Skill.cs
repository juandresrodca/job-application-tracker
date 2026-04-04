namespace JobTracker.Domain.Entities;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; } // e.g. "Cloud", "Security", "Programming"

    public ICollection<ApplicationSkill> ApplicationSkills { get; set; } = new List<ApplicationSkill>();
}

public class ApplicationSkill
{
    public int JobApplicationId { get; set; }
    public JobApplication JobApplication { get; set; } = null!;

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public bool IsOwned { get; set; }  // true = user has this skill
    public bool IsRequired { get; set; } = true;
}
