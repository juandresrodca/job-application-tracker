using System.Globalization;
using System.Windows;
using System.Windows.Input;
using JobTracker.Application.Interfaces;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.ViewModels;
using JobTracker.WPF.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.WPF;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    private string _weekInfo = string.Empty;
    public string WeekInfo
    {
        get => _weekInfo;
        set { _weekInfo = value; }
    }

    private string _weekStats = string.Empty;
    public string WeekStats
    {
        get => _weekStats;
        set { _weekStats = value; }
    }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        DataContext = this;

        var week = ISOWeek.GetWeekOfYear(DateTime.Now);
        WeekInfo = $"Week {week} · {DateTime.Now:yyyy}";
        WeekStats = "Loading...";

        NavigateCommand = new RelayCommand(Navigate);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());

        Loaded += async (_, _) =>
        {
            Navigate("Dashboard");
            await UpdateWeekStatsAsync();
        };
    }

    private void Navigate(object? parameter)
    {
        System.Windows.Controls.Page page = parameter?.ToString() switch
        {
            "Dashboard"      => CreatePage<DashboardPage>(),
            "NewApplication" => CreatePage<ApplicationFormPage>(vm =>
                                    ((ApplicationFormViewModel)vm.DataContext!).InitializeForCreateAsync()),
            "Companies"      => CreatePage<CompaniesPage>(),
            "Contacts"       => CreatePage<ContactsPage>(),
            "Skills"         => CreatePage<SkillsPage>(),
            "Settings"       => CreatePage<SettingsPage>(),
            _                => CreatePage<DashboardPage>()
        };

        MainFrame.Navigate(page);
    }

    private T CreatePage<T>() where T : System.Windows.Controls.Page
        => _services.GetRequiredService<T>();

    private T CreatePage<T>(Func<T, Task> init) where T : System.Windows.Controls.Page
    {
        var page = _services.GetRequiredService<T>();
        _ = init(page);
        return page;
    }

    private async Task RefreshAsync()
    {
        await UpdateWeekStatsAsync();
        // Refresh the current page if it has a refresh method
        if (MainFrame.Content is System.Windows.Controls.Page page &&
            page.DataContext is IRefreshable refreshable)
        {
            await refreshable.RefreshAsync();
        }
    }

    private async Task UpdateWeekStatsAsync()
    {
        try
        {
            var svc = _services.GetRequiredService<IJobApplicationService>();
            var apps = (await svc.GetCurrentWeekApplicationsAsync()).ToList();
            WeekStats = $"{apps.Count} applied · {apps.Count(a => a.Status == Domain.Enums.ApplicationStatus.Interview)} interviews";
        }
        catch
        {
            WeekStats = "—";
        }
    }

    // Allow navigation to an edit form from other pages
    public void NavigateToEdit(int applicationId)
    {
        var page = _services.GetRequiredService<ApplicationFormPage>();
        _ = ((ApplicationFormViewModel)page.DataContext!).InitializeForEditAsync(applicationId);
        MainFrame.Navigate(page);
    }
}
