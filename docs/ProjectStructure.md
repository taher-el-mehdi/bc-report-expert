# Project structure

## Repository root

```
Report Expert/
├── .github/                    # Issue/PR templates, CI
├── docs/                       # Contributor and architecture docs
├── scripts/                    # Logo, AL grammar
├── src/                        # Product source
├── tests/                      # Automated + manual smoke tests
├── Directory.Build.props       # Shared MSBuild / nullable / version
├── Directory.Packages.props    # Central NuGet versions
├── ReportExpert.slnx           # Solution
├── README.md
├── CONTRIBUTING.md
├── LICENSE
└── THIRD_PARTY_NOTICES.md
```

## `src/` projects

### Application shell

| Project | Purpose |
|---------|---------|
| `ReportExpert.App` | Startup WPF exe — Home, Workspace, ShellCoordinator, MainWindow |

### Feature modules

| Project | Purpose |
|---------|---------|
| `ReportExpert.Modules.Preview` | RDLC preview window, PDF pipeline, Settings UI |
| `ReportExpert.Modules.Copilot` | AI chat, tools, patch apply |
| `ReportExpert.Modules.Localization` | Report labels / CSV (not in nav yet) |

### Shared / core

| Project | Purpose |
|---------|---------|
| `ReportExpert.Editors` | AL Source viewer |
| `ReportExpert.Core` | Interfaces (editors, modules, shell, reporting) |
| `ReportExpert.Domain` | Shared DTOs / models |
| `ReportExpert.Common` | App-wide constants (AppData names, themes, file names) |

### Scaffolding (not hosting UI today)

| Project | Purpose |
|---------|---------|
| `ReportExpert.Application` | Use-case DI registration stubs |
| `ReportExpert.Infrastructure` | Alternate settings/dialog implementations |
| `ReportExpert.Plugins` | Plugin host / catalog |
| `ReportExpert.Shared` | Shared WPF converters |

### Reporting libraries

| Project | Purpose |
|---------|---------|
| `ReportExpert.Reporting.Parser` | RDLC parse library (unit-tested) |
| `ReportExpert.Reporting.DummyData` | Sample data generation (library) |
| `ReportExpert.Reporting.Export` | Export helpers (library) |
| `ReportExpert.Reporting.Renderer` | ReportViewer host (library) |
| `ReportExpert.Reporting.Expressions` | Expression stubs |
| `ReportExpert.RdlcDesigner` | Clean-room Minimal RDLC layout editor (WPF) |

The Preview module currently owns the **live** parser / dummy data / export used by the UI. Libraries exist for gradual consolidation.

### MCP (`src/mcp/`)

| Project | Purpose |
|---------|---------|
| `ReportExpert.Rdl.Core` | RDL document model, readers, editors, validation. No app dependencies. |
| `ReportExpert.Rdl.Mcp.Server` | The `rdlc-mcp` executable — an MCP server over stdio |
| `ReportExpert.Mcp.Client` | Client abstraction the app uses to reach any MCP server |

Independently publishable and referenced only by the shell, so the server can ship on its own — see [McpServer.md](McpServer.md).

## App folder map

```
ReportExpert.App/
├── Assets/           # Logos and icons
├── Helpers/          # Converters
├── Home/             # Start screen, layout hosts (RDLC via ReportExpert.RdlcDesigner)
├── Workspace/        # Project explorer + WorkspaceService
└── Shell/            # ShellViewModel, ShellCoordinator
```

## Tests

| Project | Role |
|---------|------|
| `tests/ReportExpert.Common.Tests` | Shared constants / helpers |
| `tests/ReportExpert.Modules.Preview.Tests` | Live Preview parser, dummy data, layout helpers, DOCX |
| `tests/ReportExpert.Modules.Copilot.Tests` | Patch parser, XML validator, provider defaults, tool flags |
| `tests/ReportExpert.Reporting.Parser.Tests` | Parser |
| `tests/ReportExpert.RdlcDesigner.Tests` | Designer persistence / undo / units |
| `tests/ReportExpert.Rdl.Core.Tests` | RDL readers, editors, validation, expressions |
| `tests/ReportExpert.Rdl.Mcp.Server.Tests` | Tool envelopes, error codes, README sync |
| `tests/ReportExpert.Mcp.Client.Tests` | End-to-end against the built `rdlc-mcp` executable |
| `tests/ReportExpert.LayoutSmoke` | Manual Excel/Word/UI smoke (not in solution CI) |

## Docs index

| Doc | Topic |
|-----|-------|
| [Architecture.md](Architecture.md) | System design |
| [GETTING_STARTED_CONTRIBUTOR.md](GETTING_STARTED_CONTRIBUTOR.md) | First contribution |
| [TESTING.md](TESTING.md) | How to run and add tests |
| [CODEBASE_ANALYSIS.md](CODEBASE_ANALYSIS.md) | Live vs scaffolding |
| [DevelopmentGuide.md](DevelopmentGuide.md) | Day-to-day setup |
| [CodingStandards.md](CodingStandards.md) | Style rules |
| [AI.md](AI.md) | Copilot architecture |
| [McpServer.md](McpServer.md) | RDLC MCP server |
| [RDLCPreviewEngine.md](RDLCPreviewEngine.md) | Preview pipeline |
| [PDFGeneration.md](PDFGeneration.md) | PDF export |
| [Roadmap.md](Roadmap.md) | Planned work |
| [FAQ.md](FAQ.md) / [Troubleshooting.md](Troubleshooting.md) | Support |
| [modules/](modules/) | Per-module notes |
