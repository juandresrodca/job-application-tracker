# Changelog

All notable changes to this project are documented here.  
Format: [Keep a Changelog](https://keepachangelog.com) · Versioning: [SemVer](https://semver.org)

---

## [Unreleased]

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
