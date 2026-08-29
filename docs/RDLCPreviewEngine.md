# RDLC Preview Engine

Describes how Report Expert loads RDLC metadata and prepares data for PDF rendering.

## Entry points

| UI | Type | Role |
|----|------|------|
| Home layout canvas | `RdlcVisualLayoutBuilder` | Visual tablix / field overview |
| Home **Preview** | `RdlcPreviewWindow` + `RdlcPreviewDialogViewModel` | Configure data + generate PDF |
| Legacy tab host | `PreviewView` / `PreviewViewModel` | Kept for LayoutSmoke / reuse — not in shell nav |

## Metadata

`RdlcMetadataParser` (Preview module) reads the RDLC XML and produces datasets, fields, parameters, page size, and expression counts used by Home and the preview dialog.

A parallel library exists at `ReportExpert.Reporting.Parser` for consolidation and unit tests.

## Data sources

Preview supports two modes (`DataSourceMode`):

1. **BC XML** — Business Central exported DataItems XML flattened to `DataTable`s (`BcXmlReportRenderer.ParseDataItems`)
2. **Sample data** — `DummyDataGenerator` invents rows from field names / types

Parameters from the RDLC can be edited in the dialog before render.

## Rendering host

ReportViewerCore `LocalReport` is used inside a WinForms host embedded in WPF. The dialog-oriented path writes PDF to disk and displays it; the legacy path can also drive an on-screen ReportViewer.

## Error handling expectations

- Missing files → clear status messages
- Schema mismatches → ReportViewer exceptions surfaced to the user
- Optional binds that throw until datasources exist may be skipped with a documented catch (see `BcXmlReportRenderer`)

## Related

- [PDFGeneration.md](PDFGeneration.md)
- [modules/preview.md](modules/preview.md)
- [Architecture.md](Architecture.md)
