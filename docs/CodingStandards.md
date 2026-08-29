# Coding standards

Report Expert follows Microsoft C# conventions, enforced lightly via `.editorconfig` and `Directory.Build.props`.

## Language

- **File-scoped namespaces** for new files
- **Nullable** enabled — annotate intentionally; avoid `!` unless justified
- Prefer `sealed` for concrete types not designed for inheritance
- Prefer `readonly` fields and init-only properties where mutation is not required
- Use `var` only when the type is obvious from the right-hand side
- Expression-bodied members only when they improve clarity (short properties/methods)

## Async

- Use `async`/`await` for I/O (files, HTTP, dialogs)
- Do not block the UI thread with `.Result` / `.Wait()`
- Use `ConfigureAwait(true)` implicitly on UI code; library code may use `ConfigureAwait(false)` when not touching WPF

## MVVM / WPF

- ViewModels inherit `ObservableObject` and use `[RelayCommand]` / `[ObservableProperty]`
- Feature UI is `UserControl` unless it is a dialog `Window`
- Prefer WPF-UI controls for shell consistency
- Converters live in module `Helpers/` (or App helpers for shell-only)

## Errors

- Never empty-catch without a comment explaining **why** failure is acceptable
- Prefer catching specific exceptions (`JsonException`, `IOException`, `HttpRequestException`)
- User-facing errors should set a status message or show a dialog — do not fail silently on critical paths
- Preference / cache load failures may reset to defaults

## Naming

| Artifact | Pattern |
|----------|---------|
| Module | `ReportExpert.Modules.{Feature}` |
| View | `{Feature}View.xaml` |
| ViewModel | `{Feature}ViewModel.cs` |
| Service | `{Purpose}Service.cs` |
| Constants | `AppConstants`, `ThemeNames`, etc. in `ReportExpert.Common` |

## Documentation

- Public types and public members: `///` summary explaining purpose / invariants
- Prefer **why** over restating the signature
- Update `docs/` when behavior changes across modules

## Dependencies

- Add NuGet versions only in `Directory.Packages.props`
- Avoid referencing one feature module from another for orchestration — use `ShellCoordinator`
- Known exception: Preview → Copilot for Settings provider fields

## Do not

- Commit API keys, `copilot.json`, Microsoft BC Base Application reports, or machine-absolute paths
- Leave large commented-out blocks
- Introduce `#region` wrappers for organization — use files and types instead
- Rewrite working logic during cleanup PRs unless fixing a clear bug
