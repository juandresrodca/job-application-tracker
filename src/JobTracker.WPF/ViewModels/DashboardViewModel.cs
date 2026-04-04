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

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<int>? EditApplicationRequested;
    public event Action? NewApplicationRequested;

    public DashboardViewModel(IJobApplicationService appService, IDialogService dialog)
    {
        _appService = appService;
        _dialog = dialog;

        CurrentWeekNumber = ISOWeek.GetWeekOfYear(DateTime.Now);

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

        // Surface sync warnings from background thread back to status bar
        _appService.SyncWarning += msg =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = $"⚠️ {msg}");
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading applications...";
        try
        {
            var all = (await _appService.GetAllApplicationsAsync()).ToList();

            Applications.Clear();
            foreach (var app in all)
                Applications.Add(app);

            TotalApplications = all.Count;
            ActiveApplications = all.Count(a => a.Status is ApplicationStatus.Applied
                or ApplicationStatus.Screening or ApplicationStatus.Interview
                or ApplicationStatus.TechnicalTest);
            InterviewCount = all.Count(a => a.Status == ApplicationStatus.Interview);

            // Response rate: at least moved past Applied
            var responded = all.Count(a => a.Status is not ApplicationStatus.Applied);
            ResponseRate = all.Count > 0
                ? $"{responded * 100 / all.Count}%"
                : "—";

            // Offer rate: received an offer or accepted
            var offers = all.Count(a => a.Status is ApplicationStatus.Offer or ApplicationStatus.Accepted);
            OfferRate = all.Count > 0
                ? $"{offers * 100 / all.Count}%"
                : "—";

            ApplyFilters();
            StatusMessage = $"Loaded {all.Count} applications — Week {CurrentWeekNumber}";
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
            var term = SearchText.ToLower();
            query = query.Where(a =>
                a.RoleName.ToLower().Contains(term) ||
                a.CompanyName.ToLower().Contains(term) ||
                (a.ContactName?.ToLower().Contains(term) ?? false));
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
