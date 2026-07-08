using System.Collections.ObjectModel;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Interfaces;

namespace JobTracker.WPF.ViewModels;

/// <summary>
/// Opt-in job discovery: fetches a company's public Greenhouse board on explicit user
/// action, ranks listings by how many catalog skills they mention, and hands picks to
/// the New Application form. No polling — the only network call is the Fetch button.
/// </summary>
public class DiscoverViewModel : ViewModelBase
{
    private readonly IJobDiscoveryService _discovery;
    private readonly ISkillRepository _skillRepo;
    private readonly IMatchScoreService _matchScore;

    public ObservableCollection<DiscoveredJobVm> Results { get; } = new();

    private string _boardSlug = string.Empty;
    public string BoardSlug
    {
        get => _boardSlug;
        set => SetField(ref _boardSlug, value);
    }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

    private string _statusMessage = "Enter a company's Greenhouse board name (e.g. \"monzo\", \"stripe\") and press Fetch. This is the app's only network call and runs only when you ask.";
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public AsyncRelayCommand FetchCommand { get; }

    /// <summary>Raised when the user wants to track a discovered job as a new application.</summary>
    public event Action<DiscoveredJobVm>? TrackRequested;

    // ── Browser co-pilot mode ────────────────────────────────────────────────
    private bool _isBrowserMode;
    public bool IsBrowserMode
    {
        get => _isBrowserMode;
        set { SetField(ref _isBrowserMode, value); OnPropertyChanged(nameof(IsBoardsMode)); }
    }
    public bool IsBoardsMode => !IsBrowserMode;

    public record SitePreset(string Name, string Url)
    {
        public override string ToString() => Name;
    }

    public IReadOnlyList<SitePreset> SitePresets { get; } = new[]
    {
        new SitePreset("Indeed (IE)", "https://ie.indeed.com/"),
        new SitePreset("IrishJobs", "https://www.irishjobs.ie/"),
        new SitePreset("Jobs.ie", "https://www.jobs.ie/"),
        new SitePreset("LinkedIn Jobs", "https://www.linkedin.com/jobs/"),
    };

    private SitePreset? _selectedSite;
    public SitePreset? SelectedSite
    {
        get => _selectedSite;
        set
        {
            SetField(ref _selectedSite, value);
            if (value is not null) { CurrentUrl = value.Url; NavigateRequested?.Invoke(value.Url); }
        }
    }

    private string _currentUrl = string.Empty;
    public string CurrentUrl { get => _currentUrl; set => SetField(ref _currentUrl, value); }

    /// <summary>The view hosts the WebView2 control; it navigates when this fires.</summary>
    public event Action<string>? NavigateRequested;

    public void RequestNavigate() => NavigateRequested?.Invoke(CurrentUrl);

    // ── Scanned page state ───────────────────────────────────────────────────
    private string? _scannedTitle;
    public string? ScannedTitle { get => _scannedTitle; private set => SetField(ref _scannedTitle, value); }

    private string? _scannedCompany;
    public string? ScannedCompany { get => _scannedCompany; private set => SetField(ref _scannedCompany, value); }

    private string? _scannedDescription;
    private string? _scannedUrl;

    private bool _hasScan;
    public bool HasScan { get => _hasScan; private set => SetField(ref _hasScan, value); }

    private string _scanMatchText = string.Empty;
    public string ScanMatchText { get => _scanMatchText; private set => SetField(ref _scanMatchText, value); }

    /// <summary>Skills mentioned on the scanned page — used for in-page highlighting.</summary>
    public IReadOnlyList<string> ScannedSkills { get; private set; } = Array.Empty<string>();

    private string _copilotStatus = "Pick a job site, open a job posting, then press Scan page.";
    public string CopilotStatus { get => _copilotStatus; set => SetField(ref _copilotStatus, value); }

    /// <summary>Called by the view after extracting the page (JSON-LD JobPosting or body text).</summary>
    public async Task ApplyScanAsync(string? title, string? company, string? description, string url)
    {
        _scannedDescription = description;
        _scannedUrl = url;
        ScannedTitle = string.IsNullOrWhiteSpace(title) ? "(no title detected)" : title.Trim();
        ScannedCompany = string.IsNullOrWhiteSpace(company) ? null : company.Trim();

        var skills = (await _skillRepo.GetAllAsync())
            .Select(s => new SkillMatchInput(s.Name, Selected: true))
            .ToList();
        var match = _matchScore.Compute(description, skills);

        ScannedSkills = match.MatchedSkills;
        ScanMatchText = match.HasDetections
            ? $"{match.MatchedSkills.Count} of your skills mentioned: {string.Join(", ", match.MatchedSkills)}"
            : "No catalog skills detected in this posting.";

        HasScan = true;
        CopilotStatus = "Scanned. Highlight your skills on the page, or track this job.";
    }

    public void TrackScanned()
    {
        if (!HasScan) return;
        var dto = new DiscoveredJobDto(0, ScannedTitle ?? "Unknown role", null,
            _scannedUrl ?? CurrentUrl, _scannedDescription, null);
        TrackRequested?.Invoke(new DiscoveredJobVm(dto, ScannedSkills));
    }

    public DiscoverViewModel(
        IJobDiscoveryService discovery,
        ISkillRepository skillRepo,
        IMatchScoreService matchScore)
    {
        _discovery = discovery;
        _skillRepo = skillRepo;
        _matchScore = matchScore;

        FetchCommand = new AsyncRelayCommand(FetchAsync, () => !string.IsNullOrWhiteSpace(BoardSlug));
    }

    private async Task FetchAsync()
    {
        IsBusy = true;
        StatusMessage = $"Fetching jobs from \"{BoardSlug.Trim()}\"…";
        Results.Clear();

        try
        {
            var jobsTask = _discovery.GetJobsAsync(BoardSlug);
            var skills = (await _skillRepo.GetAllAsync())
                .Select(s => new SkillMatchInput(s.Name, Selected: true))
                .ToList();
            var jobs = await jobsTask;

            // Rank by how many known skills each posting mentions
            var ranked = jobs
                .Select(job =>
                {
                    var match = _matchScore.Compute(job.DescriptionText, skills);
                    return new DiscoveredJobVm(job, match.MatchedSkills);
                })
                .OrderByDescending(vm => vm.RelevanceCount)
                .ThenBy(vm => vm.Title);

            foreach (var vm in ranked)
                Results.Add(vm);

            StatusMessage = Results.Count == 0
                ? $"Board \"{BoardSlug.Trim()}\" has no published jobs."
                : $"Found {Results.Count} open roles — sorted by skill relevance.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not fetch board: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RequestTrack(DiscoveredJobVm job) => TrackRequested?.Invoke(job);
}

/// <summary>Row model for a discovered job with its skill-relevance ranking.</summary>
public class DiscoveredJobVm
{
    public DiscoveredJobVm(DiscoveredJobDto dto, IReadOnlyList<string> mentionedSkills)
    {
        Dto = dto;
        MentionedSkills = mentionedSkills;
    }

    public DiscoveredJobDto Dto { get; }
    public IReadOnlyList<string> MentionedSkills { get; }

    public string Title => Dto.Title;
    public string Location => Dto.Location ?? "—";
    public string Url => Dto.Url;
    public int RelevanceCount => MentionedSkills.Count;
    public bool HasRelevance => RelevanceCount > 0;
    public string RelevanceText => RelevanceCount == 0
        ? "No catalog skills mentioned"
        : $"{RelevanceCount} skills: {string.Join(", ", MentionedSkills)}";
    public string UpdatedText => Dto.UpdatedAt?.ToString("yyyy-MM-dd") ?? "";
}
