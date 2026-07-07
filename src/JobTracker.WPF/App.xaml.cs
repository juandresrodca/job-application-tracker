using System.IO;
using System.Windows;
using JobTracker.Application.Interfaces;
using JobTracker.Application.Services;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Discovery;
using JobTracker.Infrastructure.Email;
using JobTracker.Infrastructure.Markdown;
using JobTracker.Infrastructure.Pdf;
using JobTracker.Infrastructure.Repositories;
using JobTracker.WPF.Services;
using JobTracker.WPF.ViewModels;
using JobTracker.WPF.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.WPF;

/// <summary>
/// Application entry point.
/// Wires the DI container following Clean Architecture layers:
///   WPF → Application → Domain ← Infrastructure
/// </summary>
public partial class App : System.Windows.Application
{
    private IServiceProvider _services = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers — prevent silent crashes
        DispatcherUnhandledException += (_, ex) =>
        {
            LogError("Dispatcher", ex.Exception);
            MessageBox.Show($"An unexpected error occurred:\n\n{ex.Exception.Message}",
                "Job Tracker — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            LogError("UnobservedTask", ex.Exception);
            ex.SetObserved(); // Don't crash on unobserved task exceptions
        };

        // OnStartup is async void: any exception past the first await would otherwise
        // kill the process with no dialog (it bypasses DispatcherUnhandledException).
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _services = services.BuildServiceProvider();

            // Initialize DB schema
            var db = _services.GetRequiredService<DatabaseContext>();
            await db.InitializeAsync();

            // Seed default skills
            await SeedDefaultDataAsync();

            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogError("Startup", ex);
            MessageBox.Show(
                $"Job Tracker could not start:\n\n{ex.Message}\n\nDetails were written to the log folder:\n{LogDirectory}",
                "Job Tracker — Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // ── Minimal file logging (no external dependencies) ──────────────────────
    private static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "JobTracker", "logs");

    private static void LogError(string source, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}\n";
            File.AppendAllText(Path.Combine(LogDirectory, "app.log"), line);
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Infrastructure ────────────────────────────────────────────────
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath  = Path.Combine(appData, "JobTracker", "jobtracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddSingleton(new DatabaseContext(dbPath));

        // Transient, not Scoped: no DI scopes are ever created in this app, so Scoped
        // would silently behave as Singleton. These are stateless (connection-per-call).
        services.AddTransient<IJobApplicationRepository, JobApplicationRepository>();
        services.AddTransient<ICompanyRepository,        CompanyRepository>();
        services.AddTransient<IContactRepository,        ContactRepository>();
        services.AddTransient<ISkillRepository,          SkillRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddSingleton<ISettingsService,    SettingsService>();
        services.AddTransient<IMarkdownSyncService,   MarkdownSyncService>();
        services.AddTransient<IPdfExtractionService,  PdfExtractionService>();
        services.AddTransient<IEmailExtractionService, EmailExtractionService>();
        services.AddSingleton<IMatchScoreService,  MatchScoreService>();
        services.AddSingleton<IJobDiscoveryService, GreenhouseDiscoveryService>();
        services.AddTransient<IJobApplicationService, JobApplicationService>();

        // ── WPF Services ──────────────────────────────────────────────────
        services.AddSingleton<IDialogService, WpfDialogService>();

        // ── WPF ViewModels ────────────────────────────────────────────────
        services.AddTransient<MainWindow>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ApplicationFormViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CompaniesViewModel>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<SkillsViewModel>();
        services.AddTransient<DiscoverViewModel>();

        // ── WPF Pages ─────────────────────────────────────────────────────
        services.AddTransient<DashboardPage>();
        services.AddTransient<ApplicationFormPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<CompaniesPage>();
        services.AddTransient<ContactsPage>();
        services.AddTransient<SkillsPage>();
        services.AddTransient<DiscoverPage>();
    }

    private async Task SeedDefaultDataAsync()
    {
        var skillRepo = _services.GetRequiredService<ISkillRepository>();
        var existing  = await skillRepo.GetAllAsync();
        if (existing.Any()) return; // Already seeded

        var defaultSkills = new[]
        {
            ("Azure",          "Cloud"),
            ("AWS",            "Cloud"),
            ("Google Cloud",   "Cloud"),
            ("Azure AD",       "Cloud"),
            ("Intune",         "Cloud"),
            ("M365",           "Cloud"),
            ("PowerShell",     "Scripting"),
            ("Python",         "Scripting"),
            ("Bash",           "Scripting"),
            ("Active Directory","Infrastructure"),
            ("VMware vSphere", "Virtualization"),
            ("Hyper-V",        "Virtualization"),
            ("ServiceNow",     "ITSM"),
            ("Jira",           "ITSM"),
            ("ITIL",           "Methodology"),
            ("Networking",     "Infrastructure"),
            ("Firewalls",      "Security"),
            ("SIEM",           "Security"),
            ("Defender XDR",   "Security"),
            ("Pen Testing",    "Security"),
            ("OSCP",           "Certification"),
            ("SQL",            "Data"),
            ("Docker",         "DevOps"),
            ("Kubernetes",     "DevOps"),
            ("CI/CD",          "DevOps"),
            ("Git",            "Development"),
        };

        foreach (var (name, category) in defaultSkills)
            await skillRepo.AddAsync(new Domain.Entities.Skill { Name = name, Category = category });
    }

}
