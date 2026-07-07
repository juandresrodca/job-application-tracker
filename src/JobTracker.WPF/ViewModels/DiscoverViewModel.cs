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
