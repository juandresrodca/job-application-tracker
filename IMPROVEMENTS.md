# Job Application Tracker — Improvements & Roadmap

> Analysis date: 2026-07-01 · Target: v0.1.0-beta → v0.2.0
> This document catalogs the defects found during code review, the fix approach for each,
> and the feature work that takes the app from "polished beta" to "credibly shippable v1".

Items are grouped by priority. Each has: **What**, **Where**, **Why it matters**, **Fix**.

---

## P1 — Correctness / data-safety bugs

### 1.1 YAML frontmatter injection in Obsidian sync
- **Where:** `src/JobTracker.Infrastructure/Markdown/MarkdownSyncService.cs` → `BuildMarkdown` (~line 150)
- **What:** Company/role/contact names are interpolated straight into quoted YAML:
  `company: "{app.Company?.Name}"`. A value containing a `"`, a `:` at the wrong place,
  or a newline produces invalid frontmatter that Obsidian/Dataview silently fails to parse.
  Example breakers: `KPMG "Ireland"`, `Role: Senior (Remote)\nInjected: true`.
- **Why:** Corrupts the user's vault notes — the headline feature — with no error surfaced.
- **Fix:**
  - Add a `YamlEscape(string)` helper: strip CR/LF, replace `"` with `\"` (or wrap using
    a proper YAML library such as `YamlDotNet`'s serializer for the frontmatter block).
  - Apply to every interpolated string field: `company`, `role`, `contact`, and tag values.
  - Add a unit test with adversarial names (quotes, colons, newlines, emoji).

### 1.2 Demo data seeded into the real user database
- **Where:** `src/JobTracker.WPF/App.xaml.cs` → `SeedDemoApplicationsAsync` (line 141)
- **What:** On any empty DB, the app inserts "Tech Corp / Senior Software Engineer", etc.
  A real first-run user must manually delete three fake applications and three fake companies.
- **Why:** Looks unprofessional on first launch; pollutes a genuine tracker.
- **Fix:**
  - Gate behind `#if DEBUG` **or** a `--seed-demo` command-line flag **or** a first-run
    dialog ("Load sample data so you can explore? Yes / Start empty").
  - Keep `SeedDefaultDataAsync` (the 26 skills) — that is legitimate reference data.

### 1.3 Markdown filename collisions
- **Where:** `JobApplication.MarkdownFileName` (Domain) + `MarkdownSyncService.WriteMarkdownAsync`
- **What:** Filename pattern is `<date>_<Company>_<Role>.md`. Two applications to the same
  company + role on the same day overwrite each other's file (and each other's user notes).
- **Why:** Silent data loss of user notes.
- **Fix:** Append the application `Id` to the filename (`<date>_<Company>_<Role>_<Id>.md`),
  and sanitize path-illegal characters (`/ \ : * ? " < > |`) which currently can throw or
  redirect the write. Update `DeleteApplicationFileAsync` to match.

---

## P2 — Robustness / lifecycle

### 2.1 Inconsistent sync-failure reporting (create vs. update)
- **Where:** `JobApplicationService.cs` — `CreateAsync`/`UpdateStatusAsync` use `FireAndForgetSync`;
  `UpdateAsync` awaits and reports.
- **What:** A create that fails to write markdown still reports success to the UI.
- **Fix:** Pick one policy. Recommended: keep fire-and-forget for responsiveness but ensure
  **every** path routes failures through the `SyncWarning` event (create currently does — verify
  it reaches the status bar). Document the async-sync contract in a `<summary>` on the interface.

### 2.2 Event-handler leak on `SyncWarning`
- **Where:** `DashboardViewModel.cs:181` subscribes; never unsubscribes.
- **What:** `DashboardViewModel` is `Transient`, the service is effectively singleton-scoped, so each
  new Dashboard instance adds another handler that is never released.
- **Fix:** Implement `IDisposable` on the ViewModel (or use a weak-event pattern) and unsubscribe
  when the page is navigated away. Low impact today (single window) but correct hygiene.

### 2.3 Scoped services resolved from the root provider
- **Where:** `App.xaml.cs` `ConfigureServices` — repos/services registered `Scoped`, resolved from root.
- **What:** They behave as app-lifetime singletons. Safe **only** because Dapper opens a fresh
  `SqliteConnection` per call. If EF Core or any stateful `DbContext` is introduced later, this
  becomes a cross-thread corruption bug.
- **Fix:** Either register these as `Singleton` (honest about lifetime) or introduce an explicit
  `IServiceScopeFactory` scope per user operation. Add a code comment explaining the choice.

### 2.4 `SyncAndCleanupAsync` can delete unrelated `.md` files
- **Where:** `MarkdownSyncService.SyncAndCleanupAsync` (line 85)
- **What:** It deletes every `*.md` in the vault root that doesn't match an application filename.
  If the user pointed the app at a vault root that also holds their other notes, those get deleted.
- **Why:** Catastrophic data loss.
- **Fix:** Write application files into a dedicated subfolder (e.g. `<vault>/JobApplications/`) and
  only ever scan/clean that subfolder. Never treat the vault root as owned space.

---

## P3 — Quality / testing / polish

- **Toast/snackbar notifications** — replace status-bar-only messaging (already a listed TODO).
- **ViewModel unit tests** — the test project needs `net8.0-windows`; add tests for
  `DashboardViewModel` filtering/sorting and `ApplicationFormViewModel` PDF/email extraction paths.
- **CSV / Excel export** — listed TODO; use `ClosedXML` (already proven in the sibling InventoryMapper).
- **Cancellation** — pass `CancellationToken` through service/repo async methods for long PDF parses.
- **Logging** — the app has no structured logging; add `Microsoft.Extensions.Logging` +
  a rolling file sink so beta-tester bug reports carry a log.

---

## New features (roadmap)

Ordered by value-to-effort. Each references the README's "Extending the Application" section.

### F1 — CSV / Excel export (S)
Export the applications grid to `.xlsx`. Reuse `ClosedXML`. Add an "Export" button on the Dashboard.

### F2 — Interview scheduling / calendar view (M)
- New `Interview` entity (`Id, JobApplicationId FK, ScheduledAt, DurationMin, Interviewer, Mode, Notes`).
- Migration in `DatabaseContext.InitializeAsync`.
- A calendar/timeline page with urgency indicators (reuse `Days→Urgency` converter).

### F3 — Automated email polling (L)
Replace paste-based import with an inbox scanner.
- DPAPI-encrypted OAuth token storage in `SettingsService` (`ProtectedData.Protect`).
- Gmail/Graph REST client in Infrastructure implementing `IInboxPoller`.
- Background `IHostedService`-style poller that creates **draft** applications for review
  (never auto-commits) using the existing `EmailExtractionService`.

### F4 — AI CV-improvement suggestions (M)
- `IAiService` in Application.Interfaces.
- Infrastructure impl over OpenRouter or local Ollama (default to the latest Claude model
  when a hosted provider is used).
- DPAPI-encrypted API-key storage in `SettingsService`.
- Triggered from `ApplicationFormViewModel` with job description + `CvSnapshotText`.

### F5 — Cloud sync for multi-device (L)
- `ICloudSyncService` (Azure Blob or Dropbox), triggered after each local DB write.
- Conflict strategy: last-write-wins on frontmatter, but **merge** the protected user-notes block.

---

## Suggested execution order

1. P1.2 (demo seed) + P1.1 (YAML escape) + P1.3 (filename collision) — small, high-safety.
2. P2.4 (cleanup deletes user notes) — data-loss guard.
3. F1 (export) — quick win, demoable.
4. P2.1/2.2/2.3 hygiene pass.
5. F2 (interviews) → F4 (AI) → F3/F5 (larger integrations).
