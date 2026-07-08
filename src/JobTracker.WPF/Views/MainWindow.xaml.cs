using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using JobTracker.Application.Interfaces;
using JobTracker.WPF.Interfaces;
using JobTracker.WPF.ViewModels;
using JobTracker.WPF.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.WPF;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IServiceProvider _services;

    private string _weekInfo = string.Empty;
    public string WeekInfo
    {
        get => _weekInfo;
        set { _weekInfo = value; OnPropertyChanged(); }
    }

    private string _weekStats = string.Empty;
    public string WeekStats
    {
        get => _weekStats;
        set { _weekStats = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand HelpCommand { get; }

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
        HelpCommand = new RelayCommand(ShowHelp);

        // Match the OS title bar to the dark app chrome (Win10 20H1+/Win11)
        SourceInitialized += (_, _) => TryEnableDarkTitleBar();

        Loaded += async (_, _) =>
        {
            Navigate("Dashboard");
            await UpdateWeekStatsAsync();
        };
    }

    // ── Dark window chrome ───────────────────────────────────────────────────
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void TryEnableDarkTitleBar()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (19 on early Win10 20H1 builds)
            if (DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
        }
        catch
        {
            // Cosmetic only — a light title bar is not worth crashing over.
        }
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
            "Discover"       => CreatePage<DiscoverPage>(),
            "Calendar"       => CreatePage<CalendarPage>(),
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

    private void ShowHelp()
    {
        var helpWindow = new HelpWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        helpWindow.ShowDialog();
    }

    // Allow navigation to an edit form from other pages
    public void NavigateToEdit(int applicationId)
    {
        var page = _services.GetRequiredService<ApplicationFormPage>();
        _ = ((ApplicationFormViewModel)page.DataContext!).InitializeForEditAsync(applicationId);
        MainFrame.Navigate(page);
    }

    /// <summary>Opens the New Application form pre-filled from a discovered job listing.</summary>
    public void NavigateToNewFromDiscovery(DiscoveredJobVm job)
    {
        var page = _services.GetRequiredService<ApplicationFormPage>();
        var vm = (ApplicationFormViewModel)page.DataContext!;
        _ = vm.InitializeFromDiscoveryAsync(job.Title, job.Dto.DescriptionText, job.Url);
        MainFrame.Navigate(page);
    }
}
