# Home & Workspace

**Location:** `src/ReportExpert.App/Home`, `src/ReportExpert.App/Workspace`  
**Views:** `HomeView.xaml`, `WorkspaceExplorerView.xaml`  
**View model:** `HomeWorkspaceViewModel`

## Purpose

Start screen and project workspace: open a folder, browse AL reports and layouts, edit Excel in-app, preview Word via Open XML, inspect RDLC structure, and chat with Copilot.

## Layout handling

| Kind | Home behavior | Save |
|------|---------------|------|
| **Excel** (`.xlsx`) | ReoGrid editor; Open XML read-only fallback if ReoGrid fails | In-app Save |
| **Word** (`.docx`) | `DocxFlowDocumentService` → `FlowDocument` preview | Read-only; **Open in Word** for edits |
| **RDLC** | Full-width **visual layout** (tablix columns, field tags); **Preview** opens data window | Layout on Home; data preview in window |
| **AL** (`*.Report.al`) | AvalonEdit source viewer | Read-only |

## Toolbar (RDLC)

Order on the right: Save (Excel only) → Reload → **Preview** → **Copilot**.

**Preview** opens a modal window to configure BC XML or sample data, choose the PDF output path, generate the PDF, and preview it in WebView2.

## Key types

| Type | Role |
|------|------|
| `HomeWorkspaceViewModel` | Content kind, dirty state, RDLC surface, commands |
| `HomeOfficeDocumentController` | Excel/Word host load, save, external open |
| `RdlcVisualLayoutBuilder` | Builds WPF visual layout from RDLC XML (tablix / fields) |
| `RdlcPreviewWindow` | Modal RDLC → PDF config + WebView2 preview (Preview module) |
| `WorkspaceService` | Folder scan + recent projects |
| `DocxFlowDocumentService` | Shared Word preview (Preview module) |

## Recent projects

Stored in `%AppData%\ReportExpert\recent-projects.json`.

## Related docs

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [modules/preview.md](preview.md) — data-bound RDLC preview + `DocxFlowDocumentService`
- [editors/al-source-viewer.md](../editors/al-source-viewer.md)
