# Development guide

## Prerequisites

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 17.12+ **or** VS Code / Cursor with C# Dev Kit
- Optional: Groq / OpenAI / Azure key, or [Ollama](https://ollama.com) for Copilot

## Clone and build

```bash
git clone https://github.com/taher-el-mehdi/bc-report-expert.git
cd bc-report-expert
dotnet restore ReportExpert.slnx
dotnet build ReportExpert.slnx
dotnet run --project src/ReportExpert.App
```

Set the startup project to `ReportExpert.App` in Visual Studio.

## Solution tips

- Use `ReportExpert.slnx` (Visual Studio 2022+ / `dotnet` CLI)
- Package versions: edit only `Directory.Packages.props`
- Shared TFM / nullable: `Directory.Build.props`
- Style: `.editorconfig`

## Typical workflows

### Change Preview / PDF

1. Edit services under `Modules.Preview/Services`
2. Exercise via Home → **Preview**
3. Add/adjust tests under `tests/ReportExpert.Modules.Preview.Tests` when logic is pure

### Change Copilot

1. Client: `OpenAiCompatibleClient`
2. UI state: `CopilotViewModel`
3. Shell hooks: `ShellCoordinator` (`GetCurrentXmlAsync`, `ApplyPatchHandler`, `RunPreviewAsync`)
4. Configure providers in Settings — keys stay in `%AppData%\ReportExpert\copilot.json`

### Change AL Source

1. Editor project: `ReportExpert.Editors`
2. Grammar refresh: `scripts/update-al-grammar.ps1`

## Debugging

- Attach to `ReportExpert.App`
- Preview uses WinForms ReportViewer inside WPF — check STA / dispatcher issues if hosting changes
- WebView2 needs Evergreen runtime; missing runtime falls back to the system PDF app

## Tests

```bash
dotnet test ReportExpert.slnx
dotnet run --project tests/ReportExpert.LayoutSmoke -- --home test-reports
```

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/build-release.ps1` | Self-contained publish, ZIP, installer, optional MSIX (`v1.0 Marmoset`) |
| `scripts/generate-logo-assets.py` | Logo / ICO generation |
| `scripts/update-al-grammar.ps1` | Refresh AL TextMate grammar |

## Before opening a PR

1. Build Release and Debug (or rely on CI)
2. Run tests
3. Manually verify the feature path you touched
4. Update docs if architecture or UX changed
5. Confirm no secrets or absolute local paths

More: [CONTRIBUTING.md](../CONTRIBUTING.md), [CodingStandards.md](CodingStandards.md), [Troubleshooting.md](Troubleshooting.md).
