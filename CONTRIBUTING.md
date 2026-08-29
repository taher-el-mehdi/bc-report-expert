# Contributing to Report Expert

Thanks for helping improve Report Expert. This guide is the practical path from clone to pull request.

## Fork and clone

1. Fork [bc-report-expert](https://github.com/taher-el-mehdi/bc-report-expert) on GitHub
2. Clone your fork:

```bash
git clone https://github.com/taher-el-mehdi/bc-report-expert.git
cd bc-report-expert
```

3. Add the upstream remote if you want to pull updates:

```bash
git remote add upstream https://github.com/taher-el-mehdi/bc-report-expert.git
```

## Create a branch

Branch from `main`:

| Prefix | Use |
|--------|-----|
| `feature/` | New capability |
| `fix/` | Bug fix |
| `docs/` | Documentation only |
| `refactor/` | Cleanup with no intended behavior change |
| `test/` | Tests only |
| `ci/` | Build / GitHub Actions |

Examples: `feature/copilot-provider`, `fix/preview-pdf-path`, `docs/architecture`

## Build

**Prerequisites:** Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download), Visual Studio 2022+ or VS Code / Cursor with C# support.

```bash
dotnet restore ReportExpert.slnx
dotnet build ReportExpert.slnx
dotnet run --project src/ReportExpert.App
```

Always run **`src/ReportExpert.App`** — it hosts Home and Settings. RDLC preview opens from Home as a window.

First-time setup: [docs/GETTING_STARTED_CONTRIBUTOR.md](docs/GETTING_STARTED_CONTRIBUTOR.md).

## Run tests

```bash
dotnet test ReportExpert.slnx
```

Details: [docs/TESTING.md](docs/TESTING.md). New logic in parsers, exporters, validators, or Copilot contracts should include unit tests when feasible.

## Coding conventions

Follow [docs/CodingStandards.md](docs/CodingStandards.md) and `.editorconfig`.

- Nullable reference types are enabled globally
- Prefer file-scoped namespaces, `sealed` types when inheritance is not intended
- Use `async`/`await` for I/O; keep UI work on the dispatcher
- MVVM: CommunityToolkit.Mvvm (`[RelayCommand]`, `ObservableObject`)
- XML documentation (`///`) on public types and public members — explain **why**
- Central Package Management: add versions only in `Directory.Packages.props`

## Naming conventions

| Artifact | Pattern |
|----------|---------|
| Module | `ReportExpert.Modules.{Feature}` |
| View | `{Feature}View.xaml` |
| ViewModel | `{Feature}ViewModel.cs` |
| Service | `{Purpose}Service.cs` |
| Tests | `{Type}Tests.cs` in `tests/ReportExpert.{Area}.Tests` |

Put new features in the live module that already owns that behavior. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Do not implement against `ReportExpert.Application` / `Reporting.*` unless you are doing the documented consolidation — those projects are **not** wired into the App.

## Commit expectations

Use short, imperative subjects (Conventional Commits style encouraged):

```
feat(preview): support drag-drop BC XML on preview window
fix(copilot): handle empty API key before request
docs: add RDLC preview engine guide
refactor(app): extract workspace file factory helpers
test(parser): cover nested tablix fields
```

Keep the body for **why** when the change is non-obvious. Do not commit secrets, API keys, or machine-local absolute paths.

## Pull request expectations

1. Branch from `main`
2. Keep PRs focused — one concern per PR when practical
3. Fill out `.github/PULL_REQUEST_TEMPLATE.md`
4. Ensure CI is green: `dotnet build` and `dotnet test`
5. Update docs under `docs/` when behavior or architecture changes
6. Include screenshots for UI changes
7. Do not force-push shared branches unless a maintainer asks

Reviewers look for preserved behavior, clear naming, no silent empty `catch` without a comment, and no new module-to-module orchestration (wire through `ShellCoordinator`).

## Report bugs

Use [.github/ISSUE_TEMPLATE/bug_report.md](.github/ISSUE_TEMPLATE/bug_report.md). Search existing issues first. Include Windows version, Report Expert version, steps, expected vs actual, and redacted logs.

## Propose features

Use [.github/ISSUE_TEMPLATE/feature_request.md](.github/ISSUE_TEMPLATE/feature_request.md) or Discussions **Ideas**. Lead with the problem, then a proposal. Maintainers may ask for a minimal design sketch before implementation.

## Add tests

See [docs/TESTING.md](docs/TESTING.md). Prefer xUnit tests that do not launch the UI. LayoutSmoke is manual.

## Update documentation

Docs live in [`docs/`](docs/). Prefer short, accurate pages over large rewrites. If you change architecture or UX, update Architecture / FAQ / the module page in the same PR.

## Security

- Never commit `%AppData%\ReportExpert\copilot.json` or API keys
- Prefer relative paths if you add an **original** catalog sample
- Do not commit Microsoft Business Central Base Application reports
- Validate file paths from user input before write/delete operations
- Vulnerabilities: [SECURITY.md](SECURITY.md), not a public issue

## Code of conduct

Please follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Questions?

See [docs/FAQ.md](docs/FAQ.md), open a Question issue, or contact **mehditaher01@outlook.com**.
