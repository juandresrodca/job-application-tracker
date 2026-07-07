# Changelog

All notable changes to this project are documented here.  
Format: [Keep a Changelog](https://keepachangelog.com) · Versioning: [SemVer](https://semver.org)

---

## [Unreleased]

## [0.2.0-beta] — 2026-07-07

### Added
- **Follow-up nudges** — active applications quiet for 14+ days get a ⏰ indicator in the
  table and Kanban cards, plus a "Follow up" stat card on the Dashboard
- **Offline match score** — the application form live-compares the job description against
  your skill catalog (word-boundary matching, handles CI/CD, M365, etc.) and shows
  match % and missing skills; no AI, no network
- **Job discovery (opt-in)** — new Discover page fetches a company's public Greenhouse
  board, ranks listings by skill relevance, and converts a pick into a pre-filled
  application. This is the app's only network call and runs only on explicit user action
- Regression test suites: vault safety, follow-up logic, match scoring, HTML sanitizer
  (58 tests total)
- File logging to `%APPDATA%\JobTracker\logs` for startup/dispatcher/background errors
- Landing page (GitHub Pages, `docs/`) with design polish pass

### Fixed
- **Vault safety (critical):** Obsidian sync now writes only to a `JobTracker` subfolder
  of the vault; cleanup deletes only app-owned files (marked with `USER_NOTES`) inside
  that subfolder — user notes in the vault root are never touched. Existing files are
  migrated automatically with notes preserved
- **Demo data removed:** first run no longer seeds fake companies/applications into the
  real database (default skill catalog is kept)
- Startup failures now show an error dialog instead of a silent crash
- Markdown filename collisions (same role/company/date) resolved by an Id suffix
- Email import: broken regex quantifiers meant the primary role patterns never matched
- Repository N+1 query and a discarded bulk query removed
- YAML frontmatter escaping (quotes/newlines in company/role/contact names)

### Changed
- PDF extraction switched from iText7 (AGPL) to PdfPig (Apache-2.0) — resolves a license
  conflict with this project's MIT distribution
- Markdown sync location moved from the vault root to `<vault>/JobTracker/`

## [0.1.0-beta] — 2026-04-03

### Added
- **Companies page** — full CRUD (add, edit, delete with confirmation, inline form panel, search)
- **Contacts page** — full CRUD with live LinkedIn URL validation
- **Skills page** — full CRUD, editable category combo with autocomplete
- **Kanban board view** — toggle between table and board on the Dashboard
- **Response rate + offer rate** stat cards on the Dashboard header
- **Delete confirmation dialogs** — no more accidental data loss
- `IDialogService` abstraction for testable confirmation dialogs
- Global unhandled-exception handler in `App.xaml.cs`
- xUnit test project (`JobTracker.Tests`) — service, settings, and in-memory repository tests
- GitHub Actions CI workflow (build + test on `windows-latest`)
- GitHub Actions release workflow (self-contained EXE on tag push)
- `CONTRIBUTING.md`, `CHANGELOG.md`, issue templates

### Fixed
- **Obsidian sync errors surfaced to UI** — `SyncWarning` event propagates failures to the status bar instead of silently discarding them
- `InverseBoolToVisibilityConverter` added for kanban/table view toggle

### Removed
- AI CV-analysis features (OpenRouter / Ollama integration) removed for Beta 1 — planned for a future release
- Added `settings.json` and `publish/` to `.gitignore`

---

## [0.0.1] — 2025-04-02 (initial prototype)

### Added
- Dashboard with DataGrid, search/filter/sort, stat cards
- New/Edit application form with skills selection
- AI CV analysis via OpenRouter (free tier) or Ollama (local)
- Obsidian markdown sync with `USER_NOTES` section preservation
- SQLite + Dapper backend, WAL mode, auto-schema init
- 26 default IT/security skills pre-seeded
- Dark theme (GitHub-inspired palette)
- Clean Architecture: Domain / Application / Infrastructure / WPF layers
