# Contributing to Job Application Tracker

Thank you for your interest in contributing!

## Development setup

```bash
git clone <repo-url>
cd JobTracker
dotnet restore JobTracker.sln
dotnet build JobTracker.sln
```

Run the app:
```bash
cd src/JobTracker.WPF
dotnet run
```

Run tests:
```bash
dotnet test JobTracker.sln
```

## Architecture

Clean Architecture layers — read the README for the full breakdown.  
The key rule: **inner layers never depend on outer layers.**

```
Domain ← Application ← Infrastructure
                     ← WPF
```

## Branch naming

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feature/short-description` | `feature/kanban-board` |
| Bug fix | `fix/issue-number-description` | `fix/42-delete-crash` |
| Docs / chore | `chore/description` | `chore/update-readme` |

## Commit messages (Conventional Commits)

```
feat: add CSV export to dashboard
fix: prevent delete without confirmation
docs: update AI provider setup instructions
chore: bump xunit to 2.9.0
```

## Pull request checklist

- [ ] Builds without warnings (`dotnet build`)
- [ ] All tests pass (`dotnet test`)
- [ ] No hardcoded strings that should be constants
- [ ] No new TODOs without an issue number
- [ ] No secrets or API keys in code
- [ ] README updated if behaviour changed

## No issue required for

- Typo fixes
- Small documentation improvements
- Obvious bug fixes with a failing test
