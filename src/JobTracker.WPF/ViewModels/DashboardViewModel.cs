using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.Domain.Enums;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.Services;

namespace JobTracker.WPF.ViewModels;

public class DashboardViewModel : ViewModelBase, IRefreshable
{
    private readonly IJobApplicationService _appService;
    private readonly IDialogService _dialog;

    // ── Observable collections ──────────────────────────────────────────────
    public ObservableCollection<JobApplicationDto> Applications { get; } = new();
    public ObservableCollection<JobApplicationDto> FilteredApplications { get; } = new();
    public ObservableCollection<KanbanColumnVm> KanbanColumns { get; } = new();

    // ── Filter / sort state ─────────────────────────────────────────────────
    private ApplicationStatus? _statusFilter;
    public ApplicationStatus? StatusFilter
    {
        get => _statusFilter;
        set { SetField(ref _statusFilter, value); ApplyFilters(); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { SetField(ref _searchText, value); ApplyFilters(); }
    }

    private string _sortBy = "AppliedDate";
    public string SortBy
    {
        get => _sortBy;
        set { SetField(ref _sortBy, value); ApplyFilters(); }
    }

    // ── View mode ───────────────────────────────────────────────────────────
    private bool _isKanbanView;
    public bool IsKanbanView
    {
        get => _isKanbanView;
        set { SetField(ref _isKanbanView, value); ApplyFilters(); }
    }

    private bool _isWeekView = true;
    public bool IsWeekView
    {
        get => _isWeekView;
        set
        {
            if (SetField(ref _isWeekView, value))
            {
                OnPropertyChanged(nameof(DashboardTitle));
                _ = LoadDataAsync();
            }
        }
    }

    // ── Header stats ────────────────────────────────────────────────────────
    private int _currentWeekNumber;
    public int CurrentWeekNumber
    {
        get => _currentWeekNumber;
        private set => SetField(ref _currentWeekNumber, value);
    }

    private int _totalApplications;
    public int TotalApplications
    {
        get => _totalApplications;
        private set => SetField(ref _totalApplications, value);
    }

    private int _activeApplications;
    public int ActiveApplications
    {
        get => _activeApplications;
        private set => SetField(ref _activeApplications, value);
    }

    private int _interviewCount;
    public int InterviewCount
    {
        get => _interviewCount;
        private set => SetField(ref _interviewCount, value);
    }

    private int _followUpCount;
    /// <summary>Active applications with no movement for 14+ days.</summary>
    public int FollowUpCount
    {
        get => _followUpCount;
        private set => SetField(ref _followUpCount, value);
    }

    private string _responseRate = "—";
    public string ResponseRate
    {
        get => _responseRate;
        private set => SetField(ref _responseRate, value);
    }

    private string _offerRate = "—";
    public string OfferRate
    {
        get => _offerRate;
        private set => SetField(ref _offerRate, value);
    }

    public string DashboardTitle =>
        IsWeekView
            ? $"Week {CurrentWeekNumber} — Applications"
            : "All Applications — Global Overview";

    // ── Selected item ────────────────────────────────────────────────────────
    private JobApplicationDto? _selectedApplication;
    public JobApplicationDto? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            SetField(ref _selectedApplication, value);
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedApplication is not null;

    // ── Busy / status ────────────────────────────────────────────────────────
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public IEnumerable<ApplicationStatus?> AllStatuses =>
        new ApplicationStatus?[] { null }.Concat(Enum.GetValues<ApplicationStatus>().Cast<ApplicationStatus?>());

    // ── Commands ─────────────────────────────────────────────────────────────
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public AsyncRelayCommand DeleteSelectedCommand { get; }
    public RelayCommand NewApplicationCommand { get; }
    public RelayCommand EditApplicationCommand { get; }
    public RelayCommand ToggleViewCommand { get; }
    public RelayCommand ToggleDashboardViewCommand { get; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<int>? EditApplicationRequested;
    public event Action? NewApplicationRequested;

    public DashboardViewModel(IJobApplicationService appService, IDialogService dialog)
    {
        _appService = appService;
        _dialog = dialog;

        UpdateCurrentWeekNumber();

        LoadCommand = new AsyncRelayCommand(LoadDataAsync);
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        ClearFilterCommand = new RelayCommand(ClearFilters);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync,
            () => SelectedApplication is not null);

        NewApplicationCommand = new RelayCommand(() => NewApplicationRequested?.Invoke());
        EditApplicationCommand = new RelayCommand(
            () => EditApplicationRequested?.Invoke(SelectedApplication!.Id),
            () => SelectedApplication is not null);
        ToggleViewCommand = new RelayCommand(() => IsKanbanView = !IsKanbanView);
        ToggleDashboardViewCommand = new RelayCommand(() => IsWeekView = !IsWeekView);

        // Surface sync warnings from background thread back to status bar
        _appService.SyncWarning += msg =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = $"⚠️ {msg}");
    }

    /// <summary>Updates the current week number based on today's date.</summary>
    private void UpdateCurrentWeekNumber()
    {
        int newWeekNumber = ISOWeek.GetWeekOfYear(DateTime.Now);
        if (CurrentWeekNumber != newWeekNumber)
        {
            CurrentWeekNumber = newWeekNumber;
            OnPropertyChanged(nameof(DashboardTitle));
        }
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading applications...";
        try
        {
            // Update week number in case the app crossed a week boundary
            UpdateCurrentWeekNumber();

            // Load only the required dataset based on view mode
            var source = IsWeekView
                ? (await _appService.GetCurrentWeekApplicationsAsync()).ToList()
                : (await _appService.GetAllApplicationsAsync()).ToList();

            Applications.Clear();
            foreach (var app in source)
                Applications.Add(app);

            // Compute stats over the active source
            TotalApplications = source.Count;
            ActiveApplications = source.Count(a => a.Status is ApplicationStatus.Applied
                or ApplicationStatus.Screening or ApplicationStatus.Interview
                or ApplicationStatus.TechnicalTest);
            InterviewCount = source.Count(a => a.Status == ApplicationStatus.Interview);
            FollowUpCount = source.Count(a => a.NeedsFollowUp);

            // Response rate: at least moved past Applied
            var responded = source.Count(a => a.Status is not ApplicationStatus.Applied);
            ResponseRate = source.Count > 0
                ? $"{responded * 100 / source.Count}%"
                : "—";

            // Offer rate: received an offer or accepted
            var offers = source.Count(a => a.Status is ApplicationStatus.Offer or ApplicationStatus.Accepted);
            OfferRate = source.Count > 0
                ? $"{offers * 100 / source.Count}%"
                : "—";

            ApplyFilters();

            if (IsWeekView)
                StatusMessage = $"Loaded {source.Count} applications this week";
            else
                StatusMessage = $"Loaded {source.Count} applications total";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        var query = Applications.AsEnumerable();

        if (StatusFilter.HasValue)
            query = query.Where(a => a.Status == StatusFilter.Value);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText;
            query = query.Where(a =>
                a.RoleName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.CompanyName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (a.ContactName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        query = SortBy switch
        {
            "Company"     => query.OrderBy(a => a.CompanyName),
            "Role"        => query.OrderBy(a => a.RoleName),
            "Days"        => query.OrderByDescending(a => a.DaysSinceApplication),
            "Status"      => query.OrderBy(a => a.Status),
            _             => query.OrderByDescending(a => a.AppliedDate)
        };

        var filtered = query.ToList();

        FilteredApplications.Clear();
        foreach (var app in filtered)
            FilteredApplications.Add(app);

        OnPropertyChanged(nameof(FilteredApplications));
        BuildKanbanColumns(filtered);
    }

    private void BuildKanbanColumns(List<JobApplicationDto> apps)
    {
        KanbanColumns.Clear();
        var statusOrder = new[]
        {
            ApplicationStatus.Applied,
            ApplicationStatus.Screening,
            ApplicationStatus.Interview,
            ApplicationStatus.TechnicalTest,
            ApplicationStatus.Offer,
            ApplicationStatus.Accepted,
            ApplicationStatus.Rejected,
            ApplicationStatus.Withdrawn,
        };

        foreach (var status in statusOrder)
        {
            var items = apps.Where(a => a.Status == status).ToList();
            if (items.Count == 0) continue;

            var col = new KanbanColumnVm(StatusLabel(status));
            foreach (var item in items)
                col.Items.Add(item);
            KanbanColumns.Add(col);
        }
    }

    private static string StatusLabel(ApplicationStatus s) => s switch
    {
        ApplicationStatus.TechnicalTest => "Tech Test",
        _ => s.ToString()
    };

    private void ClearFilters()
    {
        StatusFilter = null;
        SearchText = string.Empty;
        SortBy = "AppliedDate";
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedApplication is null) return;

        var confirmed = _dialog.Confirm(
            "Delete Application",
            $"Permanently delete \"{SelectedApplication.RoleName}\" at {SelectedApplication.CompanyName}?\n\nThis cannot be undone.");

        if (!confirmed) return;

        await _appService.DeleteAsync(SelectedApplication.Id);
        await LoadDataAsync();
    }

    public async Task RefreshAsync() => await LoadDataAsync();
}

/// <summary>Represents a single column in the Kanban board view.</summary>
public class KanbanColumnVm
{
    public string Title { get; }
    public ObservableCollection<JobApplicationDto> Items { get; } = new();

    public KanbanColumnVm(string title) => Title = title;
}
