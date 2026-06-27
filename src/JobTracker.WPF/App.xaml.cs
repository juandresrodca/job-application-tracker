using System.IO;
using System.Windows;
using JobTracker.Application.Interfaces;
using JobTracker.Application.Services;
using JobTracker.Domain.Interfaces;
using JobTracker.Infrastructure.Data;
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
            MessageBox.Show($"An unexpected error occurred:\n\n{ex.Exception.Message}",
                "Job Tracker — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            ex.SetObserved(); // Don't crash on unobserved task exceptions
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        // Initialize DB schema
        var db = _services.GetRequiredService<DatabaseContext>();
        await db.InitializeAsync();

        // Seed default skills and demo data
        await SeedDefaultDataAsync();
        await SeedDemoApplicationsAsync();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Infrastructure ────────────────────────────────────────────────
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath  = Path.Combine(appData, "JobTracker", "jobtracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddSingleton(new DatabaseContext(dbPath));

        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<ICompanyRepository,        CompanyRepository>();
        services.AddScoped<IContactRepository,        ContactRepository>();
        services.AddScoped<ISkillRepository,          SkillRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddSingleton<ISettingsService,    SettingsService>();
        services.AddScoped<IMarkdownSyncService,   MarkdownSyncService>();
        services.AddScoped<IPdfExtractionService,  PdfExtractionService>();
        services.AddScoped<IEmailExtractionService, EmailExtractionService>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();

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

        // ── WPF Pages ─────────────────────────────────────────────────────
        services.AddTransient<DashboardPage>();
        services.AddTransient<ApplicationFormPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<CompaniesPage>();
        services.AddTransient<ContactsPage>();
        services.AddTransient<SkillsPage>();
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

    private async Task SeedDemoApplicationsAsync()
    {
        var appService = _services.GetRequiredService<IJobApplicationService>();
        var companyRepo = _services.GetRequiredService<ICompanyRepository>();
        
        var apps = await appService.GetAllApplicationsAsync();
        if (apps.Any()) return;

        // Ensure companies exist
        var companies = await companyRepo.GetAllAsync();
        if (!companies.Any())
        {
            await companyRepo.AddAsync(new Domain.Entities.Company { Name = "Tech Corp", Industry = "Software", Location = "London" });
            await companyRepo.AddAsync(new Domain.Entities.Company { Name = "Data Solutions", Industry = "Analytics", Location = "New York" });
            await companyRepo.AddAsync(new Domain.Entities.Company { Name = "InnovateX", Industry = "AI & Robotics", Location = "Berlin" });
            companies = await companyRepo.GetAllAsync();
        }

        var techCorp = companies.First(c => c.Name == "Tech Corp");
        var dataSol = companies.First(c => c.Name == "Data Solutions");
        var innovX = companies.First(c => c.Name == "InnovateX");

        await appService.CreateAsync(new Application.DTOs.CreateJobApplicationRequest(
            "Senior Software Engineer", "Experienced C# developer needed...", techCorp.Id, null,
            Domain.Enums.ApplicationStatus.Applied, DateTime.Today.AddDays(-5), true, "£80k-£100k", "Remote position", "https://techcorp.com/jobs/1",
            new List<int>()));

        await appService.CreateAsync(new Application.DTOs.CreateJobApplicationRequest(
            "Cloud Architect", "Looking for Azure expert...", dataSol.Id, null,
            Domain.Enums.ApplicationStatus.Interview, DateTime.Today.AddDays(-2), false, "$150k", "Office based", "https://datasolutions.com/careers/2",
            new List<int>()));

        await appService.CreateAsync(new Application.DTOs.CreateJobApplicationRequest(
            "DevOps Engineer", "Kubernetes and CI/CD focus...", innovX.Id, null,
            Domain.Enums.ApplicationStatus.Offer, DateTime.Today.AddDays(-10), true, "€75k", "Great benefits", "https://innovatex.io/job/3",
            new List<int>()));
    }
}
