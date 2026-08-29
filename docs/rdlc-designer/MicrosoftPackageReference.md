# Microsoft RDLC Designer package (reference only)

The folder [`Microsoft.RdlcDesigner/`](../../Microsoft.RdlcDesigner/) is an extracted **Visual Studio Marketplace VSIX** payload:

| Field | Value |
|-------|--------|
| Display name | Microsoft RDLC Report Designer |
| Version | 15.3.1 (assembly file 15.0.1322.137) |
| Identity | `617ad572-c5b7-415c-b166-b2969077f719` |
| Target | Visual Studio Community `[15.0,17.0)` |

It contains **closed Microsoft binaries**, templates, maps, and localization — **not** designer C# source. The only `.cs` / `.vb` files are project/item template stubs.

## Why Report Expert does not ship these binaries

- Marketplace / Microsoft terms treat the offering as for **In-Scope Products** (Visual Studio).
- The designer VSIX is **not** on the Visual Studio REDIST list.
- Report Expert is MIT open source; redistributing or decompiling these assemblies into the product would be inappropriate.
- Assemblies target **.NET Framework 4.x** and depend on the Visual Studio shell (`ReportDesignerPackage`, EnvDTE, etc.).

**Policy:** keep `Microsoft.RdlcDesigner/` as a local architecture reference only. Do not reference its DLLs from any `.csproj`, do not copy IL, and do not include the folder in publish output.

Redistributable report **runtime** remains the NuGet ReportViewer / ReportViewerCore path already used by Preview — separate from the designer VSIX.

## Assembly roles (conceptual)

| Assembly | Role |
|----------|------|
| `Microsoft.ReportDesigner.dll` | VS package host (`ReportDesignerPackage`, editor factory) |
| `Microsoft.ReportDesigner.Controls.dll` | Layout designer core (`LayoutEditor`, property/schema panes) |
| Chart / Gauge / Map / RichText / RplObjectModel | Specialized design surfaces |
| `Microsoft.ReportingServices.RdlObjectModel.dll` | RDL object model |
| `Microsoft.ReportingServices.Designer.RdlDowngrade.dll` | RDL version targeting |
| `Microsoft.ReportingServices.Diagnostics.dll` | Diagnostics / service proxies |
| `$MSBuild` ReportViewer.* | Build/runtime viewer stack (not the layout editor host) |
| Project/Item templates, MapGallery, satellites | Optional IDE assets |

## VS registration (pkgdef)

- Package GUID `{f3a96850-e2ae-4e00-9278-8fe23f225a0d}` → `Microsoft.ReportDesigner.Shell.ReportDesignerPackage`
- Editor factory for `.rdlc`, tool window “Report Datasets”, RDL Expression language service
- `MapGalleryPath`, toolbox installers for ReportViewer controls

## Mapping to Report Expert clean-room interfaces

| Microsoft concept (reference) | Report Expert interface | Why needed |
|------------------------------|-------------------------|------------|
| `ILayoutEditor` / design surface | `IDesignSurface` | Hit-test, adorners, gestures |
| `IReportDesignerShell` | `IRdlcLayoutDesignerHost` | App embeds load/save/dirty |
| Report item model | `IReportItem` / `IRdlcDocument` | Stable identity for edits |
| Property browser | `IPropertyService` | Name / Value (+ geometry via gestures) |
| Undo stack | `IUndoService` | Safe editing |
| RDL load/save | `IRdlcPersistence` | Structure-preserving XML round-trip |
| Schema / toolbox / charts | Deferred | Not Minimal MVP |

See [Architecture.md](Architecture.md) for the product module design.
