# Job Application Tracker

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)
![Version](https://img.shields.io/badge/version-0.1.0--beta-orange)
![Build](https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/ci.yml/badge.svg)

A production-quality .NET 8 WPF desktop application for tracking job applications with Obsidian vault sync.

> **Beta v0.1.0** — fully functional with Companies, Contacts, Skills management, Kanban board view, PDF extraction, Obsidian sync, and a full test suite.

---

## Screenshots

### Dashboard — Table View
Track all your job applications at a glance with sortable columns and real-time status updates.

![Dashboard Table View](docs/screenshots/dashboard-table.png)

### Dashboard — Kanban Board
Organize your applications by status with an intuitive drag-and-drop Kanban workflow.

![Dashboard Kanban View](docs/screenshots/dashboard-kanban.png)

### Application Form
Comprehensive job application form with PDF extraction, skill tracking, and company/contact management.

![Application Form](docs/screenshots/application-form.png)

---

## Features (v0.1.0)

- **Companies / Contacts / Skills** — full CRUD pages with inline edit panels
- **Dashboard views** — table and Kanban board toggle
- **Application tracking** — full job application lifecycle from Applied → Interview → Offer/Rejected
- **Obsidian vault sync** — auto-sync to markdown files in your vault with protected user notes section
- **PDF extraction** — extract text and company name from job posting PDFs
- **Stat cards** — application count, response rate, and offer rate at a glance
- **Dark theme** — full dark mode support with custom resource dictionary
- **Delete confirmations** — prevent accidental data loss
- **xUnit test suite** — service, settings, and in-memory repository tests

---

## Architecture Decision: Why MVVM + WPF?

**MVVM over MVC for WPF** because:
- WPF's data binding engine is built for MVVM — `{Binding}` targets `INotifyPropertyChanged`
- ViewModels are fully testable without UI (no Window/Page references)
- Commands (`ICommand`) replace controller actions cleanly
- MVC would fight the WPF binding system, not work with it

**Clean Architecture layers:**
```
JobTracker.WPF          → Presentation (XAML, ViewModels, Converters)
JobTracker.Application  → Use Cases (Services, DTOs, Interfaces)
JobTracker.Domain       → Entities, Enums, Repository Contracts
JobTracker.Infrastructure → SQLite + Dapper, Markdown Sync, PDF Extraction
```

**Data flow:**
```
UI (XAML binding) ←→ ViewModel ←→ Application Service ←→ Repository (Dapper/SQLite)
                                                      ↘ MarkdownSyncService → .md files
                                                      ↘ PdfExtractionService → Job PDFs
```

---

## Project Structure

```
JobTracker/
├── JobTracker.sln
└── src/
    ├── JobTracker.Domain/
    │   ├── Entities/
    │   │   ├── JobApplication.cs       # Core entity, computed MarkdownFileName
    │   │   ├── Company.cs
    │   │   ├── Contact.cs
    │   │   └── Skill.cs                # + ApplicationSkill join entity
    │   ├── Enums/
    │   │   └── ApplicationStatus.cs    # Applied → Screening → Interview → Offer/Rejected
    │   └── Interfaces/
    │       └── IRepositories.cs        # Contracts: IJobApplicationRepository, etc.
    │
    ├── JobTracker.Application/
    │   ├── DTOs/
    │   │   └── Dtos.cs                 # Records: JobApplicationDto, CreateRequest
    │   ├── Interfaces/
    │   │   └── IServices.cs            # IJobApplicationService, IMarkdownSyncService, IPdfExtractionService
    │   └── Services/
    │       ├── JobApplicationService.cs
    │       └── SettingsService.cs      # JSON-persisted settings in %APPDATA%\JobTracker
    │
    ├── JobTracker.Infrastructure/
    │   ├── Data/
    │   │   └── DatabaseContext.cs      # SQLite + WAL mode + schema init
    │   ├── Repositories/
    │   │   ├── JobApplicationRepository.cs   # Dapper multi-map joins
    │   │   └── OtherRepositories.cs          # Company, Contact, Skill
    │   ├── Markdown/
    │   │   └── MarkdownSyncService.cs  # Section-aware merge (never overwrites user notes)
    │   └── Pdf/
    │       └── PdfExtractionService.cs # Extract text and metadata from job posting PDFs
    │
    └── JobTracker.WPF/
        ├── App.xaml / App.xaml.cs      # DI container + DB init + skill seeding
        ├── ViewModels/
        │   ├── ViewModelBase.cs        # INPC, RelayCommand, AsyncRelayCommand
        │   ├── DashboardViewModel.cs   # Week stats, filter/sort, CRUD commands
        │   ├── ApplicationFormViewModel.cs  # Add/Edit form + PDF extraction
        │   └── SettingsViewModel.cs    # Vault path, general app settings
        ├── Views/
        │   ├── MainWindow.xaml(.cs)    # Shell: sidebar nav + Frame
        │   └── Pages/
        │       ├── DashboardPage.xaml(.cs)
        │       ├── ApplicationFormPage.xaml(.cs)
        │       └── SettingsPage.xaml(.cs)
        ├── Converters/
        │   └── ValueConverters.cs      # Status→Color, Bool→Visibility, Days→Urgency
        └── Themes/
            └── Dark.xaml               # Full dark theme resource dictionary
```

---

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 8.0+ | https://dot.net |
| Visual Studio | 2022 17.8+ | Community (free) |
| *or* VS Code | any | + C# Dev Kit extension |
| Windows | 10/11 | WPF is Windows-only |

---

## Quick Start

### 1. Clone and open

```bash
git clone <your-repo>
cd JobTracker
```

Open `JobTracker.sln` in Visual Studio 2022, or:

```bash
cd src/JobTracker.WPF
dotnet run
```

### 2. First run

The app auto-creates:
- `%APPDATA%\JobTracker\jobtracker.db`  — SQLite database
- `%APPDATA%\JobTracker\settings.json` — your preferences
- 26 default IT/security skills pre-seeded

### 3. Configure Obsidian vault sync

Settings → Browse → select your vault folder root.

Markdown files will be written to:
```
<vault>/<YYYY-MM-DD>_<company>_<role>.md
```

Each file has structured YAML frontmatter + a protected `## User Notes` section that survives re-syncs.

---

## Database Schema

```sql
Companies       (Id, Name, Website, Industry, Location, Notes)
Contacts        (Id, Name, Email, Phone, LinkedInUrl, Role, Notes, CompanyId)
Skills          (Id, Name, Category)
JobApplications (Id, RoleName, JobDescription, Status, AppliedDate, LastUpdated,
                 JobPostingUrl, SalaryRange, IsRemote, Notes, CvSnapshotText,
                 CompanyId FK, ContactId FK)
ApplicationSkills (JobApplicationId FK, SkillId FK, IsOwned, IsRequired)
```

**Indexes:** AppliedDate, Status, CompanyId — the three most-filtered columns.

---

## Obsidian Markdown Format

```markdown
---
tags: [job-application, applied, 2025]
status: Applied
company: "KPMG Ireland"
role: "IT Infrastructure Specialist"
applied_date: 2025-04-02
contact: "Sharon Griffin"
remote: false
---

# Job Application — IT Infrastructure Specialist

## Company
**KPMG Ireland**

## Status
> **Applied**

## Timeline
| Field              | Value      |
|--------------------|------------|
| Applied Date       | 2025-04-02 |
| Days Since Applied | 3          |

## Contact
- **Name:** Sharon Griffin
- **LinkedIn:** [Profile](https://linkedin.com/...)

## Skills Required
- Azure ✅
- PowerShell ✅
- SIEM
- Defender XDR

## User Notes
<!-- USER_NOTES_START -->
_
<!-- USER_NOTES_END -->
```

The `USER_NOTES_START/END` markers protect your manual notes from being overwritten on every sync.

---

## Extending the Application

### Add email parsing
- Add `IEmailService` in Application.Interfaces
- Implement with `MailKit` in Infrastructure
- Wire a background service to poll inbox and auto-create draft applications

### Add interview scheduling
- Add a calendar/timeline view in WPF
- Add an `Interview` entity to track dates, times, and interviewer details
- Display interviews on the Dashboard with urgency indicators

### Add cloud sync
- Add `ICloudSyncService` in Application.Interfaces
- Implement with Azure Blob Storage or Dropbox API
- Trigger after every local DB write for real-time sync across devices

### Add CV improvements AI analysis
- Add `IAiService` in Application.Interfaces
- Implement with OpenRouter or Ollama in Infrastructure
- Add DPAPI-encrypted API key storage in SettingsService
- Trigger from ApplicationFormViewModel with job description and CV text

---

## Known Limitations / TODOs

- [ ] CSV / Excel export for applications
- [ ] Calendar / timeline view for interview scheduling
- [ ] Email parsing integration to auto-create applications
- [ ] Cloud sync (Dropbox / Azure Blob) for multi-device support
- [ ] AI-powered CV improvement suggestions (requires API integration)
- [ ] Toast/snackbar notifications (currently uses status bar)
- [ ] ViewModel unit tests require `net8.0-windows` test project (WPF dependency)

---

## License
MIT — use freely, adapt to your own tracker, whatever you need.
