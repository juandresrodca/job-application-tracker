using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobTracker.Application.Interfaces;

namespace JobTracker.Infrastructure.Discovery;

/// <summary>
/// Fetches published jobs from a company's public Greenhouse board.
/// Uses the keyless public API: https://boards-api.greenhouse.io/v1/boards/{slug}/jobs
/// Only called on explicit user action (the Discover page's Fetch button) — the rest
/// of the app remains fully offline.
/// </summary>
public class GreenhouseDiscoveryService : IJobDiscoveryService
{
    private const string BaseUrl = "https://boards-api.greenhouse.io/v1/boards";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public async Task<IReadOnlyList<DiscoveredJobDto>> GetJobsAsync(string boardSlug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(boardSlug))
            throw new ArgumentException("Board slug is required.", nameof(boardSlug));

        // Slugs are lowercase identifiers like "stripe" or "monzo"
        var slug = boardSlug.Trim().ToLowerInvariant();
        var url = $"{BaseUrl}/{Uri.EscapeDataString(slug)}/jobs?content=true";

        using var response = await Http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new HttpRequestException($"No public Greenhouse board found for \"{slug}\".");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GreenhouseJobsResponse>(cancellationToken: ct);
        if (payload?.Jobs is null) return Array.Empty<DiscoveredJobDto>();

        return payload.Jobs
            .Where(j => !string.IsNullOrWhiteSpace(j.Title) && !string.IsNullOrWhiteSpace(j.AbsoluteUrl))
            .Select(j => new DiscoveredJobDto(
                j.Id,
                j.Title!.Trim(),
                j.Location?.Name,
                j.AbsoluteUrl!,
                HtmlToPlainText(j.Content),
                j.UpdatedAt))
            .ToList();
    }

    /// <summary>Greenhouse returns the posting body as HTML-encoded HTML — decode and strip to plain text.</summary>
    internal static string? HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var text = WebUtility.HtmlDecode(html);
        text = Regex.Replace(text, @"<br\s*/?>|</p>|</div>|</li>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<.*?>", string.Empty, RegexOptions.Singleline);
        text = WebUtility.HtmlDecode(text); // content is often double-encoded
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"(\r?\n\s*){3,}", "\n\n");
        return text.Trim();
    }

    // ── Wire format ──────────────────────────────────────────────────────────
    private sealed class GreenhouseJobsResponse
    {
        [JsonPropertyName("jobs")] public List<GreenhouseJob>? Jobs { get; set; }
    }

    private sealed class GreenhouseJob
    {
        [JsonPropertyName("id")]           public long Id { get; set; }
        [JsonPropertyName("title")]        public string? Title { get; set; }
        [JsonPropertyName("absolute_url")] public string? AbsoluteUrl { get; set; }
        [JsonPropertyName("updated_at")]   public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("content")]      public string? Content { get; set; }
        [JsonPropertyName("location")]     public GreenhouseLocation? Location { get; set; }
    }

    private sealed class GreenhouseLocation
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
