using System.Text.RegularExpressions;
using JobTracker.Application.Interfaces;

namespace JobTracker.Application.Services;

/// <summary>
/// Computes a fully offline match score between a job description and the user's
/// skill matrix: which known skills does the posting mention, and how many of those
/// does the user have ticked? No AI, no network — consistent with the privacy pitch.
/// </summary>
public class MatchScoreService : IMatchScoreService
{
    public MatchScoreResult Compute(string? jobDescription, IEnumerable<SkillMatchInput> skills)
    {
        if (string.IsNullOrWhiteSpace(jobDescription))
            return MatchScoreResult.Empty;

        var matched = new List<string>();
        var missing = new List<string>();

        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill.Name)) continue;
            if (!MentionsSkill(jobDescription, skill.Name)) continue;

            if (skill.Selected) matched.Add(skill.Name);
            else missing.Add(skill.Name);
        }

        var detected = matched.Count + missing.Count;
        if (detected == 0)
            return MatchScoreResult.Empty;

        return new MatchScoreResult(
            ScorePercent: matched.Count * 100 / detected,
            MatchedSkills: matched,
            MissingSkills: missing);
    }

    /// <summary>
    /// Word-boundary match so "Git" doesn't fire on "digital". Skills containing
    /// non-word characters (CI/CD, M365) are matched with lookarounds instead of \b,
    /// which misbehaves next to symbols.
    /// </summary>
    private static bool MentionsSkill(string text, string skillName)
    {
        var pattern = $@"(?<![\w]){Regex.Escape(skillName)}(?![\w])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }
}
