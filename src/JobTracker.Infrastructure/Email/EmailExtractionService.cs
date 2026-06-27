using System.Text.RegularExpressions;
using JobTracker.Application.Interfaces;

namespace JobTracker.Infrastructure.Email;

public class EmailExtractionService : IEmailExtractionService
{
    public EmailExtractionResult Extract(string rawEmailText)
    {
        if (string.IsNullOrWhiteSpace(rawEmailText))
            return new EmailExtractionResult(null, null, null, null, null);

        var roleName    = ExtractRole(rawEmailText);
        var companyName = ExtractCompany(rawEmailText);
        var date        = ExtractDate(rawEmailText);
        var url         = ExtractUrl(rawEmailText);

        return new EmailExtractionResult(roleName, companyName, date, url, rawEmailText.Trim());
    }

    // ── Role ─────────────────────────────────────────────────────────────────

    private static string? ExtractRole(string text)
    {
        // "your application for <Role> at|with Company"
        var m = Regex.Match(text,
            @"application\s+(?:for\s+(?:the\s+)?|to\s+the\s+position\s+of\s+)([^,\n@]{3,80?}?)\s+(?:at|with|to|@)\s+",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // "applied for the <Role> position"
        m = Regex.Match(text,
            @"applied\s+for\s+(?:the\s+)?([^,\n]{3,80?}?)\s+(?:position|role|job|opportunity)",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // "Position: <Role>" or "Role: <Role>" or "Job Title: <Role>"
        m = Regex.Match(text,
            @"(?:^|\n)\s*(?:Position|Role|Job\s+Title|Title|Vacancy)\s*[:\-]\s*([^\n]{3,80})",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // Subject line: "Application Confirmation - <Role>" or "Re: <Role> at Company"
        m = Regex.Match(text,
            @"Subject\s*[:\-]\s*(?:Re\s*:\s*)?(?:Application\s+(?:Confirmation|Received|for)\s*[-–:]\s*)?([^\n]{3,80}?)(?:\s+at\s+|\s*\n|$)",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var candidate = Clean(m.Groups[1].Value);
            // Reject generic subject lines
            if (!Regex.IsMatch(candidate, @"thank you|confirmation|received|your application", RegexOptions.IgnoreCase))
                return candidate;
        }

        return null;
    }

    // ── Company ───────────────────────────────────────────────────────────────

    private static string? ExtractCompany(string text)
    {
        // "application to/at/with <Company>"
        var m = Regex.Match(text,
            @"(?:application\s+(?:to|at|with)|applying\s+to|interest\s+in\s+joining)\s+([A-Z][^\n,\.]{1,60}?)(?:\s*[,\.\n!]|\s+for\s+|\s+as\s+)",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // "Thank you for applying to <Company>"
        m = Regex.Match(text,
            @"(?:thank(?:s|\s+you)\s+for\s+(?:applying|your\s+application|your\s+interest)\s+(?:to|at|with))\s+([A-Z][^\n,\.!]{1,60}?)(?:\s*[,\.\n!])",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // "From: <Display Name> <email>" — company name often in display name
        m = Regex.Match(text,
            @"(?:^|\n)From\s*:\s*(.+?)\s*<[^>]+>",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var candidate = Clean(m.Groups[1].Value);
            // Skip generic recruiter/no-reply names
            if (!Regex.IsMatch(candidate, @"no.?reply|talent|recruit|careers|hiring|jobs|hr\b|team\b", RegexOptions.IgnoreCase)
                && candidate.Length >= 2)
                return candidate;
        }

        // "Company: <Name>" label
        m = Regex.Match(text,
            @"(?:^|\n)\s*(?:Company|Employer|Organization|Hiring\s+Company)\s*[:\-]\s*([^\n]{2,80})",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        // "at <Company>, we" — common opener
        m = Regex.Match(text,
            @"\bat\s+([A-Z][^\n,\.]{1,60}?),\s+we\b",
            RegexOptions.IgnoreCase);
        if (m.Success) return Clean(m.Groups[1].Value);

        return null;
    }

    // ── Date ─────────────────────────────────────────────────────────────────

    private static DateTime? ExtractDate(string text)
    {
        // Email header "Date: Mon, 23 Jun 2026 10:30:00 +0000"
        var m = Regex.Match(text,
            @"(?:^|\n)Date\s*:\s*(.{10,50})",
            RegexOptions.IgnoreCase);
        if (m.Success && DateTime.TryParse(m.Groups[1].Value.Trim(), out var headerDate))
            return headerDate.Date;

        // ISO date in text: 2026-06-23
        m = Regex.Match(text, @"\b(\d{4}-\d{2}-\d{2})\b");
        if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var isoDate))
            return isoDate.Date;

        // "June 23, 2026" or "23 June 2026"
        m = Regex.Match(text,
            @"\b(\d{1,2}\s+(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{4})\b",
            RegexOptions.IgnoreCase);
        if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var longDate1))
            return longDate1.Date;

        m = Regex.Match(text,
            @"\b((?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2},?\s+\d{4})\b",
            RegexOptions.IgnoreCase);
        if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var longDate2))
            return longDate2.Date;

        return null;
    }

    // ── URL ───────────────────────────────────────────────────────────────────

    private static string? ExtractUrl(string text)
    {
        // Prefer job-specific URLs
        var m = Regex.Match(text,
            @"https?://[^\s<>""']+?(?:jobs?|careers?|positions?|openings?|apply|vacancy)[^\s<>""']*",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Value.TrimEnd('.');

        // Any https URL as fallback
        m = Regex.Match(text, @"https?://[^\s<>""']{10,}");
        if (m.Success) return m.Value.TrimEnd('.');

        return null;
    }

    private static string Clean(string value) =>
        Regex.Replace(value.Trim(), @"\s{2,}", " ");
}
