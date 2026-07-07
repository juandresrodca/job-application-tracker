# Code Audit & Feature Research — JobTracker Beta 1

> **Date:** 2026-07-07 · **Scope:** full source review (bugs, security, bad practices) + GitHub research for feature ideas.
> Companion to `BETA-1-LAUNCH-PLAN.md`.
>
> **UPDATE 2026-07-07:** All findings below have been **patched** (Critical 1–2, High 3–5,
> Medium 6–8 partially — logging added at the WPF layer; Low 10 quick items). Verified:
> full solution builds clean, **34/34 tests pass**, including 5 new regression tests in
> `MarkdownSyncServiceTests.cs` covering the vault-safety rules. Part 3 features not yet started.

---

## Part 1 — Bugs & Risks (ranked)

### 🔴 CRITICAL 1 — `SyncAndCleanupAsync` can delete the user's personal Obsidian notes
`MarkdownSyncService.cs:105-124` — the cleanup step deletes **every `*.md` file in the vault root** that doesn't match a current application:
```csharp
var markdownFiles = Directory.GetFiles(VaultPath, "*.md");
...
File.Delete(filePath);   // any .md not owned by the app is "orphaned"
```
The README tells users to select their **vault folder root**. Anyone who does that and triggers sync-and-cleanup will have their unrelated personal notes **permanently deleted** (no recycle bin). This is the most dangerous line in the app.
**Fix direction:** write to a dedicated subfolder (`<vault>/JobTracker/`), and/or only delete files containing the app's own `USER_NOTES` markers, and/or send to Recycle Bin + confirmation dialog listing files first.

### 🔴 CRITICAL 2 — Demo/fake data is seeded into every real user's database
`App.xaml.cs:141-177` — `SeedDemoApplicationsAsync()` inserts 3 fake companies ("Tech Corp", "Data Solutions", "InnovateX") and 3 fake applications on first run, **unconditionally, in production**. Every beta user starts with fabricated job applications mixed into their real tracker — and they sync to their Obsidian vault as real-looking `.md` files.
**Fix direction:** remove the call, or gate it behind `#if DEBUG` / a `--demo` flag / a "Load sample data" button in Settings.

### 🟠 HIGH 3 — `async void OnStartup`: startup crashes are unhandled
`App.xaml.cs:27` — `OnStartup` is `async void`; exceptions after the first `await` (DB init, seeding) bypass `DispatcherUnhandledException` and kill the process with no dialog. A locked/corrupt DB file = app silently never opens.
**Fix direction:** wrap the awaited startup body in try/catch with a MessageBox + graceful shutdown.

### 🟠 HIGH 4 — Markdown filename collisions silently overwrite applications
`JobApplication.cs:31-32` — `MarkdownFileName` = date + company + role. Two applications for the **same role at the same company on the same day** (or names that sanitize to the same string, e.g. all-emoji/Unicode names → empty) produce the same filename; the second sync overwrites the first, including its preserved user notes.
**Fix direction:** append the application `Id` to the filename.

### 🟠 HIGH 5 — Broken regex quantifier in `EmailExtractionService`
`EmailExtractionService.cs:27,33` — `[^,\n@]{3,80?}?` is not a valid quantifier (`{3,80?}` — the `?` belongs *after* the brace). .NET silently treats the malformed brace as **literal text**, so the first two role-extraction patterns effectively never match. The feature limps along on the weaker fallback patterns.
**Fix direction:** change to `{3,80}?` in both patterns; add unit tests with real confirmation-email samples.

### 🟡 MEDIUM 6 — iText7 license conflict (legal, not technical)
The app is MIT-licensed and distributed as a binary, but **iText7 is AGPL**. Distributing it inside an MIT app without releasing under AGPL (or buying a commercial license) is a license violation.
**Fix direction:** swap to **PdfPig** (Apache-2.0) or **PDFsharp** (MIT) — extraction needs here are simple text-dump + regex, easily covered.

### 🟡 MEDIUM 7 — Dead + N+1 query code in `GetAllWithDetailsAsync`
`JobApplicationRepository.cs:52-80` — runs one bulk ApplicationSkills query whose **result is discarded**, then loops **per application** re-querying skills (N+1). Works, but wasteful and confusing.
**Fix direction:** keep the bulk query, group by `JobApplicationId` in memory, delete the loop.

### 🟡 MEDIUM 8 — YAML/Markdown injection breaks synced notes
`MarkdownSyncService.BuildMarkdown` interpolates company/role/contact strings straight into YAML frontmatter (`company: "{name}"`). A quote in a name (`ACME "The Best" Ltd`) produces invalid YAML; Obsidian shows a broken properties panel. Not exploitable (local data), but a correctness bug.
**Fix direction:** escape `"` and newlines in frontmatter values.

### 🟡 MEDIUM 9 — Exceptions swallowed silently, and no logging anywhere
- `TaskScheduler.UnobservedTaskException` → `SetObserved()` only: background sync failures vanish.
- `SettingsService.Load` → bare `catch { }`: corrupt settings silently reset.
- No log file at all — beta bug reports will be "it crashed", with nothing to attach.
**Fix direction:** minimal file logger (`%APPDATA%\JobTracker\logs\`) wired into all three spots + the dispatcher handler.

### 🟢 LOW 10 — Assorted
| Where | Issue |
|---|---|
| `DashboardViewModel.cs:209` | `var all = IsWeekView ? source : null;` dead variable |
| `DashboardViewModel.ApplyFilters` | `ToLower().Contains` → use `Contains(term, StringComparison.OrdinalIgnoreCase)` (allocation-free, culture-safe) |
| `JobApplication.DaysSinceApplication` | mixes `UtcNow` with locally-entered dates → off-by-one around midnight |
| `App.xaml.cs` DI | repos registered `AddScoped` but resolved from root provider — they behave as singletons; register as `AddTransient` or actually create scopes |
| `WriteMarkdownAsync` | sync `File.ReadAllText` inside async path (`ExtractUserNotes`) — fine at this scale, inconsistent style |
| Dispatcher exception handler | can spawn an unbounded MessageBox loop if a render-cycle error repeats |

### ✅ What's genuinely good (keep doing this)
- **SQL injection: none** — Dapper parameterization used consistently throughout.
- Path traversal into the vault blocked — `SanitizeFileName` strips both slashes.
- FK enforcement OK — `Microsoft.Data.Sqlite` enables `foreign_keys` per-connection by default.
- WAL mode, indexes on the right columns, transactions around multi-statement writes.
- No network calls at all — the "100% offline" claim on the landing page is true.

---

## Part 2 — GitHub research (similar projects)

Surveyed via search + scrape on 2026-07-07 (raw data in `.firecrawl/`):

| Project | Stack | Stars | Notable ideas |
|---|---|---|---|
| **[Gsync/jobsync](https://github.com/Gsync/jobsync)** | Next.js, self-hosted | ~714★ | Resume management + PDF export/import, **automated job discovery (Greenhouse API) with local relevance scoring**, task/activity tracking with time tracking, interview **Question Bank**, analytics dashboard, MCP server for AI agents |
| **[santifer/career-ops](https://github.com/santifer/career-ops)** | CLI, local AI | — | Scan job portals, **score listings A–F against your CV**, tailor CV per application |
| **[Gsync topic: job-application-tracker](https://github.com/topics/job-application-tracker)** | 56 repos | — | Only **1 C# project** in the whole topic — a native Windows desktop tracker is a genuinely underserved niche (differentiator for the blog) |
| Various (JobMatchAI, jobhunt, ReachOut…) | web | — | Chrome-extension capture, match-score vs resume, reminder nudges |

**Positioning insight:** almost everything in this space is a self-hosted web app. JobTracker's local-first, no-Docker, single-EXE story is unique — lean into that in the blog.

---

## Part 3 — Three proposed features (mapped to the existing architecture)

### Feature 1 — Follow-up reminders & activity timeline ⭐ recommended first
*Inspired by: JobSync task/activity management; fills the existing README TODO "calendar/timeline view".*
Track every event (applied, emailed, interview, offer) per application and surface "needs attention" nudges: **"No response in 14 days → follow up"**, upcoming interviews on the dashboard, overdue chips on cards.
- `Domain`: new `ActivityEvent` entity (`Id, JobApplicationId, Type, Date, Notes`) + `FollowUpDate` on `JobApplication`.
- `Infrastructure`: new table + repo; nudge query is a simple date filter.
- `WPF`: timeline panel on the form; "Attention" stat card + urgency converter already exists (`DaysToUrgency`).
- Effort: **S–M**. Zero new dependencies, huge daily-use value.

### Feature 2 — CV/Resume versions per application + match score
*Inspired by: JobSync resume management, career-ops A–F scoring.*
Store multiple CV versions (the `CvSnapshotText` column already exists!), attach one per application, and compute a **local keyword match score** between the job description and the skills matrix + CV text — no AI/API needed, pure string analysis: "You match 7/10 required skills; missing: Kubernetes, SIEM."
- `Application`: `MatchScoreService` (tokenize description ↔ Skills table + CV text).
- `WPF`: score badge on the form and dashboard column; "missing skills" list feeds directly into the existing skills checkboxes.
- Effort: **M**. Differentiator: works fully offline, consistent with the privacy pitch.

### Feature 3 — Job discovery from Greenhouse boards (opt-in, still local-first)
*Inspired by: JobSync's automated discovery — the standout feature of the space.*
Let the user "watch" companies; poll the **public Greenhouse boards API** (no key required) for their open roles, rank them against the user's skills/target titles locally, and show a "Discovered" inbox → one click converts a listing into an application.
- `Infrastructure`: `GreenhouseClient` (single public JSON endpoint per company), `IJobDiscoveryService`.
- `Domain`: `WatchedCompany`, `DiscoveredJob` entities.
- `WPF`: "Discover" page + convert-to-application command.
- Effort: **M–L**. Note: this adds the app's *first* network call — keep it strictly opt-in so the "100% offline by default" claim stays honest (toggle in Settings, off by default).

**Suggested order:** 1 → 2 → 3 (rising effort, each builds audience for the next blog post).

---

## Suggested VS Code prompts for the top fixes

**Critical 1 (vault deletion):**
```
In src/JobTracker.Infrastructure/Markdown/MarkdownSyncService.cs, SyncAndCleanupAsync
deletes every *.md in the vault root that doesn't match an application — it can destroy
the user's personal Obsidian notes. Change the service to write all files into a
"JobTracker" subfolder of the vault (create if missing), migrate existing app-owned
files there, and restrict orphan cleanup to that subfolder only, and only for files that
contain the "<!-- USER_NOTES_START -->" marker. Update tests.
```

**Critical 2 (demo data):**
```
In src/JobTracker.WPF/App.xaml.cs, remove the unconditional SeedDemoApplicationsAsync()
call from OnStartup. Keep the method but expose it as a "Load sample data" button in the
Settings page (SettingsViewModel + SettingsPage.xaml) so users opt in. Keep skill seeding.
```

**High 3 (startup crash):**
```
In App.xaml.cs, OnStartup is async void — exceptions after the first await crash the app
with no message. Wrap the body after base.OnStartup in try/catch: on failure show a
MessageBox with the error and call Shutdown(1). Also log to
%APPDATA%\JobTracker\logs\startup.log.
```

**High 4 (filename collision):** append `_{app.Id}` to `MarkdownFileName` in `JobApplication.cs` (and handle renames of previously synced files).

**High 5 (regex):** in `EmailExtractionService.cs` lines 27 and 33 replace `{3,80?}?` with `{3,80}?`; add xUnit cases with sample confirmation emails.

**Medium 6 (license):** replace iText7 with PdfPig in `PdfExtractionService` (same extract-text-per-page loop), remove the iText packages, verify with the existing PdfExtractionServiceTests.
