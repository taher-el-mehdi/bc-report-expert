# Report Expert - REX

**v1.0 Marmoset** — Windows desktop toolkit for RDLC report development.

![Report Expert logo](src/ReportExpert.App/Assets/logo.png)


### Download

From [GitHub Releases](https://github.com/taher-el-mehdi/bc-report-expert/releases):

| Artifact | File |
|----------|------|
| Installer (recommended) | `ReportExpert-Setup-v1.0-Marmoset.exe` |
| Portable ZIP | `ReportExpert-v1.0-Marmoset-win-x64.zip` |

The installer is per-user (no admin). The ZIP is self-contained — unzip and run `ReportExpert.exe`. Windows 10/11 x64 is required; 

## Main features

- **Workspace** — open a project folder; inspect RDLC layouts, AL source, Excel, and Word
- **RDLC preview** — generate sample data and preview as PDF
- **Layout designer** — clean-room editor (toolbox, data/params, expressions, lines/images)
- **Copilot (Rex)** — edits reports through approved tool calls
- **Settings** — theme, preview defaults, Copilot providers

## Architecture overview

```
src/
  ReportExpert.App/                 # WPF shell (startup)
  ReportExpert.Modules.Preview/     # Preview window + Settings
  ReportExpert.Modules.Copilot/     # AI assistant
  ReportExpert.Editors/             # AL Source viewer
  ReportExpert.RdlcDesigner/        # Minimal clean-room RDLC layout editor
  ReportExpert.Core|Domain|Common/  # Contracts and shared models
  mcp/                              # RDLC MCP server, its RDL library, and the MCP client
```

## Requirements

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source
- Optional: an AI provider API key, or [Ollama](https://ollama.com) locally

## How to build

```bash
git clone https://github.com/taher-el-mehdi/bc-report-expert.git
cd bc-report-expert
dotnet restore ReportExpert.slnx
dotnet build ReportExpert.slnx -c Release
dotnet run --project src/ReportExpert.App
```

### Release packages

From the repo root, publish a self-contained win-x64 build plus portable ZIP, Inno Setup installer (if [Inno Setup 6](https://jrsoftware.org/isinfo.php) is installed), and optional MSIX:

```powershell
.\scripts\build-release.ps1
```

Artifacts land in `artifacts\`:

| Artifact | Path |
|----------|------|
| Portable ZIP | `artifacts\ReportExpert-v1.0-Marmoset-win-x64.zip` |
| Installer | `artifacts\installer\ReportExpert-Setup-v1.0-Marmoset.exe` |
| MSIX | `artifacts\msix\ReportExpert-v1.0-Marmoset-x64.msix` |

Version display (`1.0`) and codename (`Marmoset`) come from `Directory.Build.props`.

## How to contribute

See [CONTRIBUTING.md](CONTRIBUTING.md), [docs/GETTING_STARTED_CONTRIBUTOR.md](docs/GETTING_STARTED_CONTRIBUTOR.md), and [docs/DevelopmentGuide.md](docs/DevelopmentGuide.md).

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

Roadmap: [docs/Roadmap.md](docs/Roadmap.md). Docs index: [docs/README.md](docs/README.md).

## License

[MIT](LICENSE) — Copyright 2026 TAHER El Mehdi
