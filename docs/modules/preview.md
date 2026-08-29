# Preview Module

**Project:** `src/ReportExpert.Modules.Preview`  
**Primary UI:** `Views/Dialogs/RdlcPreviewWindow.xaml` (opened from Home)  
**Settings view:** `Views/SettingsView.xaml` (shell Settings tab)

## Purpose

Configure RDLC → PDF generation (Business Central XML data or sample rows), render with ReportViewerCore, and preview the PDF in WebView2. Preview is **not** a nav tab — open it from Home with the **Preview** button (RDLC layouts only).

The PDF pipeline is adapted from [RDLC Report Tester](https://github.com/stackcollider/rdlc-report-tester) (`BcXmlReportRenderer`).

See also: [Home & Workspace](home.md), [AL Source Viewer](../editors/al-source-viewer.md)

## Folder structure

```
ReportExpert.Modules.Preview/
├── Export/              ExportFormat enum (legacy ReportViewer export path)
├── Generators/          DummyDataGenerator — field-name inference
├── Helpers/             Value converters, DialogService
├── Models/              RdlcReportMetadata, RdlcDataSetInfo, AppSettings, …
├── Rendering/           ReportViewerHost — WinForms interop (legacy tab)
├── Services/
│   ├── BcXmlReportRenderer.cs   BC XML → DataTable → LocalReport PDF
│   ├── RdlcMetadataParser.cs
│   ├── ReportPreviewService.cs
│   ├── ReportExportService.cs
│   ├── OfficeLayoutPreviewService.cs
│   ├── DocxFlowDocumentService.cs
│   ├── SettingsService.cs
│   └── RecentFilesService.cs
├── ViewModels/
│   ├── RdlcPreviewDialogViewModel.cs   Home preview / PDF config window
│   ├── PreviewViewModel.cs             Legacy full-tab UI (kept for reuse)
│   ├── ParameterEditorViewModel.cs
│   └── SettingsViewModel.cs
└── Views/
    ├── Dialogs/RdlcPreviewWindow.xaml  Modal PDF config + WebView2 preview
    ├── SettingsView.xaml
    ├── PreviewView.xaml                Legacy tab host (not in shell nav)
    └── Dialogs/ErrorDialog.xaml
```

## Home → configure & generate PDF

1. User opens an `.rdlc` on Home
2. Clicks **Preview** (toolbar, next to Copilot)
3. `RdlcPreviewWindow` opens with:
   - Report details (page size, datasets, parameters)
   - **Data source:** BC XML (exported DataItems) or **Sample data**
   - Auto-suggest nearby `.xml` next to the RDLC; drag-and-drop supported
   - **Save PDF as** path (defaults beside the XML / report / export folder)
   - Report parameters (when present)
   - **Generate PDF** → writes PDF via `BcXmlReportRenderer`, shows it in WebView2
   - **Data** tab — denormalized XML / sample grid + selected-row field inspector

Copilot’s `run_preview` tool opens the same window.

## Important APIs

### `RdlcPreviewDialogViewModel`

- `InitializeAsync(path)` — parse metadata, suggest XML/output paths
- `GeneratePdfCommand` — BC XML or sample tables → PDF → `PdfReady` event
- `BrowseXmlCommand` / `BrowseOutputCommand` — file dialogs
- `SetXmlPath` — used by drag-and-drop

### `BcXmlReportRenderer`

- `ParseDataItems(xmlPath)` — BC nested DataItems → flat `DataTable` with type inference
- `RenderToPdf(rdlc, xml, output)` — full BC XML path
- `RenderTablesToPdf(rdlc, tables, output, parameters?)` — shared PDF writer

### `RdlcMetadataParser.Parse(string rdlcPath)`

Returns report metadata with datasets, fields, parameters, page dimensions, expression count.

### `DummyDataGenerator.GenerateTable(dataset, rowCount)`

Used when **Sample data** mode is selected.

## Settings

Stored in `%AppData%\ReportExpert\settings.json`: theme, font size, rows generated, export folder, Copilot provider fields.  
WebView2 user data lives under `%AppData%\ReportExpert\WebView2`.

## Extending

- **Better dummy data:** extend `DummyDataGenerator` heuristics
- **Multi-dataset BC XML:** extend `BcXmlReportRenderer.ParseDataItems` beyond the single denormalized table
- **Shared models:** migrate `Models/` to `ReportExpert.Domain` (see ARCHITECTURE.md)
