# Testing

## How to run tests

From the repository root, on Windows with the .NET 10 SDK:

```bash
dotnet test ReportExpert.slnx
```

Release (matches CI):

```bash
dotnet test ReportExpert.slnx -c Release
```

One project:

```bash
dotnet test tests/ReportExpert.Modules.Copilot.Tests
dotnet test tests/ReportExpert.Rdl.Core.Tests
```

Manual layout smoke (not part of `dotnet test` / CI):

```bash
dotnet run --project tests/ReportExpert.LayoutSmoke -- --home test-reports
```

CI runs restore → build → test on `windows-latest` with .NET 10 (`/.github/workflows/ci.yml`).

## Framework

The solution uses **xUnit** (`xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`) via Central Package Management. Add new tests with the same packages. Do not introduce a second unit-test framework.

WPF modules need `TargetFramework=net10.0-windows` and `UseWPF` on the test project (see Preview and Copilot tests).

## How to add a test

1. Prefer an **existing** test project next to the code under test.
2. If the production project has no tests yet, add `tests/ReportExpert.{Area}.Tests` and a line in `ReportExpert.slnx`.
3. Name tests after behavior: `Parse_MinimalReport_ReturnsDatasetFieldsAndPageSize`.
4. Keep tests free of UI (`Window.Show`, STA dispatchers) unless you are extending LayoutSmoke.
5. Use temp files under `%TEMP%` and delete them in `finally` / `IDisposable`.
6. Do not read `%AppData%\ReportExpert\copilot.json` or real API keys.
7. For internals, add `<InternalsVisibleTo Include="…Tests" />` on the production csproj (already used by MCP and Preview/Copilot).

## What should be tested

Prioritize logic that can break reports or contributor workflows **without** launching WPF:

| Area | Examples | Project |
|------|----------|---------|
| RDL parse / edit / validate | Readers, editors, expression parser | `Rdl.Core.Tests` |
| MCP tools | Envelopes, error codes, backups, round-trip | `Rdl.Mcp.Server.Tests` |
| MCP client | Config JSON, tool name qualify/split, filter | `Mcp.Client.Tests` |
| Live Preview parser + dummy data | Field types, BC flag names, CSV export | `Modules.Preview.Tests` |
| Copilot contracts | Patch parser, XML validator, provider defaults, tool enable flags | `Modules.Copilot.Tests` |
| Designer persistence | Units, XML round-trip, undo | `RdlcDesigner.Tests` |
| Shared constants | AppData file names | `Common.Tests` |
| Scaffold parser | Until Preview consolidates onto it | `Reporting.Parser.Tests` |

## What should not require UI testing

- Dummy data generation and type inference
- RDLC XML parse / patch / validate
- Settings serialization shapes (use temp paths)
- MCP tool handlers
- Export format name mapping

Exercise these with xUnit. Use LayoutSmoke or a manual checklist for:

- MainWindow navigation
- WebView2 PDF display
- ReoGrid Excel editing
- Designer drag/drop
- Copilot streaming + approval buttons

## How contributors can verify a change

1. `dotnet build ReportExpert.slnx`
2. `dotnet test ReportExpert.slnx`
3. If you touched Preview / Copilot / Home: run the app (`dotnet run --project src/ReportExpert.App`) and follow the feature path
4. If you touched MCP tools: `Rdl.Core.Tests` + `Rdl.Mcp.Server.Tests` + `Mcp.Client.Tests` (E2E needs the built `rdlc-mcp.exe`)
5. Update this file or module docs if you add a new test project

## Coverage philosophy

There is **no** 100% coverage target. Aim for confidence in parsers, dummy data, MCP edits, and Copilot contracts. Untested on purpose until someone owns them: WPF chrome, WebView2, ReportViewer hosting, ReoGrid.

## Important untested areas (known)

- `BcXmlReportRenderer` / ReportViewer PDF (needs WinForms host)
- `CopilotViewModel` / `RexAgentLoop` against a live provider
- `ShellCoordinator` / `McpHost` process lifetime
- Localization module
- Scaffolding Application / Infrastructure / Plugins
