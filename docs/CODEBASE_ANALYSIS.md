# Codebase analysis (Phase 1)

Snapshot of Report Expert as inspected for open-source readiness. **No deletions were made based on this document alone.** Scaffolding and dynamically loaded surfaces are treated as “keep until proven unused.”

**Product:** Windows WPF desktop app (.NET 10) for RDLC report development — workspace editing, PDF preview with sample or Business Central XML data, and multi-provider AI Copilot with MCP tools.

**Solution:** `ReportExpert.slnx`  
**Remote:** `https://github.com/taher-el-mehdi/bc-report-expert.git`

---

## Solution structure

```
Report Expert/
├── .github/                 Issue/PR templates, CI
├── docs/                    Architecture and contributor guides
├── installer/               Inno Setup + MSIX
├── scripts/                 Logo, AL grammar
├── src/                     Product source
├── tests/                   xUnit + LayoutSmoke
├── Assets/                  Packaging logo / setup icon
├── Directory.Build.props    Shared TFM / nullable / version
├── Directory.Packages.props Central NuGet versions
└── ReportExpert.slnx
```

### Projects

| Folder | Project | Role | Wired into App? |
|--------|---------|------|-----------------|
| `src/app` | `ReportExpert.App` | WPF shell, Home, Workspace, MCP host | **Yes** |
| `src/modules` | `Modules.Preview` | Preview window, PDF, Settings | **Yes** |
| | `Modules.Copilot` | AI chat, agent loop | **Yes** |
| | `Modules.Localization` | Label/CSV editor | **No** |
| `src/core` | `Core` | Interfaces | Partial (editors + workspace) |
| | `Domain` | Shared DTOs | Partial (workspace models) |
| | `Common` | AppData names, themes | **Yes** |
| | `Editors` | AL Source viewer | **Yes** |
| | `Application` | Use-case DI stubs | **No** |
| | `Infrastructure` | Alternate settings/dialogs | **No** |
| | `Plugins` | Plugin host (reflection) | **No** |
| | `Shared` | Shared converters | **No** |
| `src/reporting` | `Reporting.Parser` etc. | Parallel reporting libraries | **No** (Parser tested only) |
| | `RdlcDesigner` | Clean-room layout editor | **Yes** |
| `src/mcp` | `Rdl.Core` | RDL parse/edit/validate | Via MCP server |
| | `Rdl.Mcp.Server` | `rdlc-mcp` stdio process | Child process |
| | `Mcp.Client` | MCP client used by Copilot | **Yes** |

### Major namespaces (live path)

- `ReportExpert.App` / `.Shell` / `.Home` / `.Workspace`
- `ReportExpert.Modules.Preview` / `.Copilot`
- `ReportExpert.Editors`, `ReportExpert.RdlcDesigner`
- `ReportExpert.Common`, `ReportExpert.Domain.Models`, `ReportExpert.Core.Editors`
- `ReportExpert.Mcp.Client`, `ReportExpert.Rdl.Core`, `ReportExpert.Rdl.Mcp.Server`

---

## Important services

Live composition is **manual `new`**, not Microsoft.Extensions.DI.

| Service | Constructed where |
|---------|-------------------|
| `WorkspaceService` | `MainWindow` |
| `SettingsService` (Preview) | `MainWindow`, `App.ApplySavedTheme` |
| `EditorServiceBootstrap` | `MainWindow` → `EditorServicesLocator` |
| `ShellCoordinator` + `McpHost` | `MainWindow` |
| `CopilotViewModel` / `OpenAiCompatibleClient` | Copilot view |
| `RexAgentLoop` | Per agent turn |
| `McpToolRegistry` | `McpHost` |
| `RdlcLayoutDesignerHost` | Home view |

**DI exists but App never calls it:** `Application.AddApplication`, `Infrastructure.AddInfrastructure`. The MCP **server** process uses `Host.CreateApplicationBuilder` (separate executable).

**Service locator:** `EditorServicesLocator` (static) for AL editor theme/load/syntax.

---

## UI structure

- **Entry:** `App.xaml` → `MainWindow.xaml` (`FluentWindow`, WPF-UI)
- **Nav:** Home · Settings
- **Home:** welcome / recent projects, RDLC designer, Excel (ReoGrid), Word preview, AL source, embedded Copilot
- **Dialogs:** `RdlcPreviewWindow` (PDF / WebView2), designer `ExpressionEditorWindow`
- **Not in nav:** `PreviewView` (full preview module UI), `LocalizationView`

---

## Dependency injection

| Layer | Status |
|-------|--------|
| App shell | Manual construction |
| Core interfaces | Defined; Preview concretes often **do not implement** them |
| Application / Infrastructure | Registration methods unused |
| MCP server | Real DI via MCP SDK host |

---

## External dependencies

Central versions in `Directory.Packages.props`. See [DEPENDENCIES.md](DEPENDENCIES.md).

| Package | Live use |
|---------|----------|
| WPF-UI | Shell and modules |
| CommunityToolkit.Mvvm | ViewModels |
| AvalonEdit + TextMateSharp | AL editor |
| ReportViewerCore.NETCore / WinForms | PDF render |
| Microsoft.Web.WebView2 | PDF viewer |
| DocumentFormat.OpenXml | Word layouts |
| unvell.ReoGridWPF.dll | Excel edit |
| ModelContextProtocol | MCP client + server |
| Microsoft.Extensions.Hosting | MCP server only |

---

## Database / file access

**No database.** JSON, XML, and files only.

| Location | Contents |
|----------|----------|
| `%AppData%\ReportExpert\settings.json` | Theme, preview defaults |
| `%AppData%\ReportExpert\copilot.json` | Provider, **API key** (plaintext), model |
| `%AppData%\ReportExpert\recent.json` | Recent RDLC files |
| `%AppData%\ReportExpert\recent-projects.json` | Recent folders |
| `%AppData%\ReportExpert\mcp-servers.json` | MCP server list |
| `%AppData%\ReportExpert\WebView2\` | WebView2 profile |
| `%TEMP%\ReportExpert\Preview\` | Generated PDFs |
| `{workspace}\.report_expert\chat\` | Copilot history |

---

## RDLC / reporting

**Two stacks:**

1. **Live UI:** Preview module parser / dummy data / export / ReportViewer; `RdlcDesigner` for layout editing; Home preview dialog → `BcXmlReportRenderer` → PDF.
2. **MCP:** `ReportExpert.Rdl.Core` readers/editors/validator used by `rdlc-mcp` (not by the designer surface directly).
3. **Scaffold:** `Reporting.Parser`, `DummyData`, `Export`, `Renderer`, `Expressions` — parallel libraries, not referenced by App.

---

## AI / LLM

- Providers: Groq, Gemini, Mistral, OpenRouter, Claude, OpenAI, Azure OpenAI, Ollama
- HTTP: `OpenAiCompatibleClient` (`/chat/completions` + tool calling)
- Agent: `RexAgentLoop` (max 8 iterations) → MCP tools via `IMcpToolGateway`
- Approval: `IToolApprovalService` (writes only)
- Settings: `CopilotSettingsService` → `copilot.json`

Legacy (not currently called from `CopilotViewModel`): `CopilotToolExecutor`, `RdlcPatchParser`, `RdlcPatch`. Documented in [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md).

---

## Configuration

- No `appsettings.json` for the WPF host
- User JSON under AppData (above)
- MCP seeded by `McpHost.EnsureConfiguration` to bundled `mcp\rdlc\rdlc-mcp.exe`
- Build: `Directory.Build.props`; MCP folder tightens warnings-as-errors

---

## Logging

| Area | Behavior |
|------|----------|
| WPF App | No `ILogger`; crashes → MessageBox |
| Mcp.Client | Source-generated `Log` (App passes none → NullLogger) |
| rdlc-mcp | stderr only (stdout is JSON-RPC) |
| Plugins | `ILogger<PluginHost>` (unused host) |

---

## Background services / tasks

No `IHostedService` in the WPF app. Async work: `Task.Run` for parse/render, designer debounce timers, settings auto-save (~350 ms), MCP processes started lazily, `async void` UI handlers.

---

## Important application flows

### Startup

`App` registers code-page encodings and theme → `MainWindow` constructs workspace + editor bootstrap + `ShellCoordinator` (MCP seed + Copilot gateway) → Settings feature.

### Preview PDF

Home Preview → `RdlcPreviewWindow` → sidecar JSON or dummy `DataTable`s → `BcXmlReportRenderer` → temp PDF → WebView2 (system viewer fallback).

### Copilot agent

User message → context + history → list MCP tools → model tool calls → approval for writes → `McpToolRegistry.CallToolAsync` → reload file → persist chat.

---

## Existing tests

| Project | Coverage |
|---------|----------|
| `Common.Tests` | AppData / theme constants |
| `Modules.Preview.Tests` | DOCX → FlowDocument |
| `Reporting.Parser.Tests` | **Scaffold** parser, not live Preview parser |
| `RdlcDesigner.Tests` | Persistence, units, schema/edit |
| `Rdl.Core.Tests` | Read/edit/validate/expressions |
| `Rdl.Mcp.Server.Tests` | Tool catalogue, envelopes, safety, round-trip |
| `Mcp.Client.Tests` | Config, filter, E2E against `rdlc-mcp` |
| `LayoutSmoke` | Manual STA smoke (not in solution CI) |

Framework: **xUnit**. Thin: App shell, Copilot, live Preview parser/dummy data, Localization.

---

## Build configurations

- TFMs: `net10.0` (libraries/MCP) / `net10.0-windows` (WPF)
- Debug / Release; CI: Windows + .NET 10.x Release (`/.github/workflows/ci.yml`)
- App post-build copies `rdlc-mcp` → `$(OutDir)mcp\rdlc\`
- MCP: `TreatWarningsAsErrors=true`

---

## A. Definitely used code

- App: `App`, `MainWindow`, `ShellCoordinator`, `McpHost`, `ShellViewModel`, `EditorServiceBootstrap`, Home + Workspace types
- Preview: Settings, `RdlcPreviewWindow`, `BcXmlReportRenderer`, Office helpers, live parser/dummy data/export used by the preview dialog
- Copilot: `CopilotView`/`ViewModel`, `OpenAiCompatibleClient`, `RexAgentLoop`, settings, markdown helpers, summarizer, chat history, filtering gateway
- Editors: AL viewer + TextMate
- RdlcDesigner (hosted on Home)
- Common: `AppConstants`, `ThemeNames`
- Domain: workspace models
- Core: editor interfaces + `IWorkspaceService`
- MCP client + bundled server + Rdl.Core

---

## B. Potentially unused (dynamic / planned)

| Item | Why keep |
|------|----------|
| `PreviewView` / `PreviewViewModel` | Used by `LayoutSmoke`; not in MainWindow nav |
| `RecentFilesService` | Tied to PreviewView-centric UX |
| Extra MCP servers in `mcp-servers.json` | Config-driven |
| `PluginHost` + `plugin.json` | Reflection-based; **not called by App** |
| XAML converters / DataTemplates | Bound by name |
| MCP `[McpServerTool]` methods | SDK `WithToolsFromAssembly()` |
| Application / Infrastructure / Reporting.* | Roadmap consolidation; deleting would block that migration |
| Localization module | Documented as unwired, intended product feature |
| `CopilotToolExecutor` / `RdlcPatchParser` / `RdlcPatch` | Legacy patch path; may be rewired as non-agent fallback |

---

## C. Probably dead code

These have **no callers** in App/modules and no XAML/reflection discovery. **Not deleted** — see [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md).

| Location | Symbols |
|----------|---------|
| `Modules.Copilot/Services/CopilotToolExecutor.cs` | Entire type |
| `Modules.Copilot/Services/RdlcPatchParser.cs` | Entire type |
| `Modules.Copilot/Models/RdlcPatch.cs` | Entire type |
| `Core/Modules/ModuleCatalog.cs` | `ModuleCatalog` |
| `Core/Shell/ShellBuilder.cs` | `ShellBuilder` registrations |
| `Core/Documents/IDocumentManager.cs` | Unused interface |
| `Core/Services/IDialogService.cs` (`INavigationService`) | Unused |
| `Domain/Documents/IDocument.cs` | Unused |
| `Shared/Converters` | Project unused by App |
| `Plugins/*` | Never constructed |
| `Application/*` use cases | Never registered |
| `Infrastructure/*` | Duplicate of Preview services |
| `Reporting.Expressions` | Stubs (`Evaluate` returns the expression string) |

No `TODO`/`FIXME`/`HACK` in `src`. No hardcoded API keys. No `C:\Users\...` paths in source.

---

## D. Technical debt

1. Dual architecture: Clean Architecture projects vs manual App composition
2. Duplicated models/services: Domain vs Preview; Reporting.* vs Preview; converters copied per module
3. Large types: `RdlcVisualRenderer`, `RdlcLayoutDesignerView`, `PreviewViewModel`, `PropertyBrowserPanel`, `CopilotViewModel`, `DocumentEditService`
4. Magic strings: provider URLs/models, Azure `YOUR-RESOURCE` placeholder, MCP tool names
5. Empty/best-effort `catch` on preference files and some UI paths
6. No structured logging in the WPF host
7. Copilot keys stored plaintext in AppData
8. Orphan `PreviewView` vs Home preview dialog
9. Parser tests cover the scaffold parser, not the live Preview parser (addressed in this readiness pass)

---

## E. Architectural risks for contributors

1. Implementing against Core/Application/Reporting.* **does not change the running app**
2. No DI in App — easy to add another `new FooService()`
3. Two preview UX paths; smoke tests exercise the unused one
4. MCP post-build copy is fragile (“no tools” if copy incomplete)
5. Agent writes on disk vs designer in-memory buffer (reload races)
6. Windows-only + ReportViewer + WinForms hosting
7. Plugin / Localization / scaffolding look like finished features
8. Uneven quality bar: MCP warnings-as-errors vs App `TreatWarningsAsErrors=false`

---

## Related documents

- [ARCHITECTURE.md](ARCHITECTURE.md) — where to add features
- [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md) — items not deleted
- [TESTING.md](TESTING.md)
- [OPEN_SOURCE_READINESS.md](OPEN_SOURCE_READINESS.md)
