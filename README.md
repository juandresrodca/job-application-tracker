# Job Application Tracker

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue?logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)
![Version](https://img.shields.io/badge/version-0.1.0--beta-orange)
![Build](https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/ci.yml/badge.svg)

A production-quality .NET 8 WPF desktop application for tracking job applications, syncing to Obsidian, and getting AI-powered CV analysis.

> **Beta v0.1.0** — fully functional with Companies, Contacts, Skills management, Kanban board view, DPAPI-encrypted API keys, and a full test suite.

---

## Screenshots

> _Add screenshots here after first run. Use Win + Shift + S to capture, save to `docs/screenshots/`._

| Dashboard (Table) | Dashboard (Kanban) | Application Form |
|---|---|---|
| ![dashboard](docs/screenshots/dashboard-table.png) | ![kanban](docs/screenshots/dashboard-kanban.png) | ![form](docs/screenshots/application-form.png) |

---

## What's New in Beta (v0.1.0)

- **Companies / Contacts / Skills** — full CRUD pages with inline edit panels
- **Kanban board** — toggle between table and board view on the Dashboard
- **Response rate & Offer rate** stat cards
- **Delete confirmations** — no accidental data loss
- **DPAPI-encrypted API key** — OpenRouter key is never stored in plaintext
- **Sync error surfacing** — Obsidian sync failures shown in status bar
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
JobTracker.Application  → Use Cases (Services, DTOs, Interfaces, AI Prompts)
JobTracker.Domain       → Entities, Enums, Repository Contracts
JobTracker.Infrastructure → SQLite + Dapper, Markdown Sync, AI HTTP clients
```

**Data flow:**
```
UI (XAML binding) ←→ ViewModel ←→ Application Service ←→ Repository (Dapper/SQLite)
                                                      ↘ MarkdownSyncService → .md files
                                  AI Service ←→ OpenRouter / Ollama HTTP API
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
    │   │   └── Dtos.cs                 # Records: JobApplicationDto, CreateRequest, AiAnalysisResult
    │   ├── Interfaces/
    │   │   └── IServices.cs            # IJobApplicationService, IMarkdownSyncService, IAiService
    │   ├── Prompts/
    │   │   └── AiPromptTemplates.cs    # Centralised prompt engineering
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
    │   └── AI/
    │       └── AiServices.cs           # OpenRouterAiService + OllamaAiService + Factory
    │
    └── JobTracker.WPF/
        ├── App.xaml / App.xaml.cs      # DI container + DB init + skill seeding
        ├── ViewModels/
        │   ├── ViewModelBase.cs        # INPC, RelayCommand, AsyncRelayCommand
        │   ├── DashboardViewModel.cs   # Week stats, filter/sort, CRUD commands
        │   ├── ApplicationFormViewModel.cs  # Add/Edit form + AI analysis trigger
        │   └── SettingsViewModel.cs    # Vault path, AI config, CV text
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

### 3. Configure AI (optional but recommended)

**Option A — OpenRouter (free, online):**
1. Register at https://openrouter.ai (no credit card)
2. Create an API key
3. In the app: Settings → paste key → select model `mistralai/mistral-7b-instruct:free`

**Option B — Ollama (offline, private):**
1. Install Ollama: https://ollama.ai
2. `ollama pull mistral`
3. In app Settings → Provider → `ollama`

### 4. Configure Obsidian sync

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

## AI Prompt Design

The prompt in `AiPromptTemplates.cs` enforces structured JSON output:

```json
{
  "missingSkills": ["SIEM", "Defender XDR"],
  "cvImprovements": [
    "Change 'Managed infrastructure' → 'Reduced MTTR 40% by automating patch deployment across 300 VMs using PowerShell DSC'"
  ],
  "courseRecommendations": [
    "Microsoft SC-200 — Microsoft Learn (free)",
    "TryHackMe SOC Level 1 — TryHackMe (free tier available)"
  ],
  "summary": "Strong infrastructure match. Main gap is SIEM/SOAR tooling. SC-200 is your highest-ROI next cert."
}
```

**Why this prompt works:**
- Forces JSON → no parsing failures from conversational preamble
- Separates concern (skills vs CV language vs courses)
- Specifies free-first for course recommendations
- Uses domain-aware framing (IT infra + security context)

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
_Add your personal notes here._
<!-- USER_NOTES_END -->
```

The `USER_NOTES_START/END` markers protect your manual notes from being overwritten on every sync.

---

## Adding a New AI Provider

1. Implement `IAiService` in `Infrastructure/AI/`
2. Add a case to `AiServiceFactory.Create()`
3. Add the option to `SettingsViewModel.AiProviders`

No other code changes required — the rest of the system is interface-bound.

---

## Extending the Application

### Add email parsing (future)
- Add `IEmailService` in Application.Interfaces
- Implement with `MailKit` in Infrastructure
- Wire a background service to poll inbox and auto-create draft applications

### Add analytics
- Add a `DashboardAnalyticsViewModel`
- Query aggregates from `IJobApplicationRepository`
- Display with OxyPlot or LiveCharts2 (both free/OSS)

### Add cloud sync
- Add `ICloudSyncService`
- Implement with Azure Blob Storage or Dropbox API
- Trigger after every local DB write

---

## Known Limitations / TODOs

- [ ] No CSV / PDF export
- [ ] No calendar / timeline view for interview scheduling
- [ ] No email parsing integration
- [ ] No cloud sync (Dropbox / Azure Blob)
- [ ] ViewModel unit tests need `net8.0-windows` test project (WPF dependency)
- [ ] No toast/snackbar notifications (uses status bar text)

---

## License
MIT — use freely, adapt to your own tracker, sell as a product, whatever you need.
