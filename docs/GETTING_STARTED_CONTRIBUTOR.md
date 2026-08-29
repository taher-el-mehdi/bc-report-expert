# Contributor getting started

If you discovered Report Expert on GitHub and want to contribute a feature tomorrow, start here.

## The first five things to understand

1. **The running app is `ReportExpert.App`.** Home and Settings live there. Preview is a window from Home, not a nav tab.
2. **Live code vs scaffolding.** Preview / Copilot / Editors / RdlcDesigner / `src/mcp` are live. `Application`, `Infrastructure`, `Plugins`, `Shared`, `Reporting.*`, and Localization are in the solution but **not** what the UI calls. Edit the live path unless you are doing the consolidation in [Roadmap.md](Roadmap.md).
3. **Modules must not orchestrate each other.** Wire cross-feature behavior through `ShellCoordinator`. Copilot talks to MCP via `IMcpToolGateway`, never via a reference to `rdlc-mcp`.
4. **Settings and secrets are local.** `%AppData%\ReportExpert\copilot.json` holds API keys. Never commit it. Examples: [examples/copilot.example.json](examples/copilot.example.json).
5. **Tests are xUnit on Windows.** `dotnet test ReportExpert.slnx` is the gate. UI chrome is manual / LayoutSmoke.

## Environment setup

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 17.12+ **or** VS Code / Cursor with C# Dev Kit
- Optional: WebView2 Evergreen runtime (PDF in-window; otherwise the system viewer)
- Optional: an AI provider key, or [Ollama](https://ollama.com)

```bash
git clone https://github.com/taher-el-mehdi/bc-report-expert.git
cd bc-report-expert
```

## Project structure

See [ProjectStructure.md](ProjectStructure.md). Short map:

| Path | What it is |
|------|------------|
| `src/ReportExpert.App` | WPF shell — start here for navigation / workspace |
| `src/ReportExpert.Modules.Preview` | PDF preview, sample data, Settings |
| `src/ReportExpert.Modules.Copilot` | Rex chat + agent loop |
| `src/ReportExpert.RdlcDesigner` | Layout editor hosted on Home |
| `src/mcp/` | RDL library + `rdlc-mcp` + client |
| `tests/` | xUnit projects + LayoutSmoke |
| `docs/` | Architecture and module notes |

## First build

```bash
dotnet restore ReportExpert.slnx
dotnet build ReportExpert.slnx
dotnet run --project src/ReportExpert.App
```

Set the startup project to `ReportExpert.App` in Visual Studio.

## First test

```bash
dotnet test ReportExpert.slnx
```

A small, fast slice:

```bash
dotnet test tests/ReportExpert.Common.Tests
dotnet test tests/ReportExpert.Modules.Copilot.Tests
```

## Where features belong

Use the table in [ARCHITECTURE.md](ARCHITECTURE.md#where-to-add-a-new-feature). Examples:

- New Copilot provider default → `CopilotProvider` / `CopilotProviderDefaults`
- New RDLC edit tool → `src/mcp/ReportExpert.Rdl.Mcp.Server/Tools` + tests in `Rdl.Core` / `Rdl.Mcp.Server`
- New Settings toggle → Preview `AppSettings` + `SettingsView` (persist via `SettingsService`)
- New Home editor host → `HomeWorkspaceViewModel` + a control; do not add a nav item unless it is a top-level feature

## Debugging

- Attach to `ReportExpert.exe` / `ReportExpert.App`
- Preview hosts WinForms ReportViewer inside WPF — STA / dispatcher bugs show up here
- MCP: the child process logs to **stderr**; stdout is JSON-RPC
- Copilot: keys in AppData; a failed test of the key in Settings does not print the key

## Submitting a PR

Follow [CONTRIBUTING.md](../CONTRIBUTING.md). Keep the diff focused, run tests, do not commit secrets.

## Good first issues (suggested labels)

These are sized for a new contributor. They are suggestions for maintainers to file with `good first issue`:

1. Add README screenshots (Home, Preview, Copilot) once a public build exists
2. Document one MCP tool with a before/after XML snippet in [McpServer.md](McpServer.md)
3. Surface a clearer Settings message when WebView2 is missing (copy already exists on the preview window)
4. Add XML docs on a Preview public type that currently relies on `CS1591` suppression
5. Replace a duplicated converter in one module with a comment pointing at `ReportExpert.Shared` (do not migrate all modules in one PR)
6. Fix `TextMate.cs` CS8625 (null literal to non-nullable) in Editors
7. Add a FAQ entry for “agent mode does nothing” (MCP server not copied to output)
8. Add an xUnit test for `LayoutFileHelper` edge cases already listed in Preview tests as a pattern to copy

## Related

- [DevelopmentGuide.md](DevelopmentGuide.md)
- [TESTING.md](TESTING.md)
- [CODEBASE_ANALYSIS.md](CODEBASE_ANALYSIS.md)
