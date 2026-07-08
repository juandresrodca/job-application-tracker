using System.Collections.ObjectModel;
using JobTracker.Application.DTOs;
using JobTracker.Application.Interfaces;
using JobTracker.WPF.Interfaces;

namespace JobTracker.WPF.ViewModels;

/// <summary>Month-grid calendar of scheduled interviews (Feature: interview calendar).</summary>
public class CalendarViewModel : ViewModelBase, IRefreshable
{
    private readonly IInterviewService _interviewService;

    private DateTime _currentMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public ObservableCollection<CalendarDayVm> Days { get; } = new();

    public string MonthTitle => _currentMonth.ToString("MMMM yyyy");

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public AsyncRelayCommand PreviousMonthCommand { get; }
    public AsyncRelayCommand NextMonthCommand { get; }
    public AsyncRelayCommand TodayCommand { get; }

    public CalendarViewModel(IInterviewService interviewService)
    {
        _interviewService = interviewService;

        PreviousMonthCommand = new AsyncRelayCommand(() => ShiftMonthAsync(-1));
        NextMonthCommand = new AsyncRelayCommand(() => ShiftMonthAsync(1));
        TodayCommand = new AsyncRelayCommand(() =>
        {
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            OnPropertyChanged(nameof(MonthTitle));
            return LoadAsync();
        });
    }

    private Task ShiftMonthAsync(int months)
    {
        _currentMonth = _currentMonth.AddMonths(months);
        OnPropertyChanged(nameof(MonthTitle));
        return LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            var interviews = await _interviewService.GetForMonthAsync(_currentMonth.Year, _currentMonth.Month);
            var byDay = interviews
                .GroupBy(i => i.ScheduledAt.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.ScheduledAt).ToList());

            // 6x7 grid starting on the Monday of the week containing the 1st
            int offsetToMonday = ((int)_currentMonth.DayOfWeek + 6) % 7;
            var gridStart = _currentMonth.AddDays(-offsetToMonday);

            Days.Clear();
            for (int i = 0; i < 42; i++)
            {
                var date = gridStart.AddDays(i);
                Days.Add(new CalendarDayVm(
                    date,
                    isCurrentMonth: date.Month == _currentMonth.Month,
                    interviews: byDay.TryGetValue(date.Date, out var list)
                        ? list
                        : new List<InterviewDto>()));
            }

            StatusMessage = interviews.Count == 0
                ? $"No interviews scheduled in {MonthTitle}. Add them from an application's edit form."
                : $"{interviews.Count} interview(s) in {MonthTitle}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public Task RefreshAsync() => LoadAsync();
}

/// <summary>One cell of the calendar month grid.</summary>
public class CalendarDayVm
{
    public CalendarDayVm(DateTime date, bool isCurrentMonth, IReadOnlyList<InterviewDto> interviews)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        Interviews = interviews;
    }

    public DateTime Date { get; }
    public bool IsCurrentMonth { get; }
    public IReadOnlyList<InterviewDto> Interviews { get; }

    public int DayNumber => Date.Day;
    public bool IsToday => Date.Date == DateTime.Today;
    public bool HasInterviews => Interviews.Count > 0;
}
