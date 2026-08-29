# ReportExpert.RdlcDesigner — components

Clean-room RDLC layout editor. Isolated, testable, and extensible without Visual Studio.

| Component | Path | Why needed |
|-----------|------|------------|
| `IRdlcLayoutDesignerHost` | `Abstractions/` | Single App integration surface: load/save/dirty/undo |
| `IRdlcDocument` / `IReportItem` | `Abstractions/` | Stable item identity for selection and edits |
| `IRdlcPersistence` | `Abstractions/` | Structure-preserving XML I/O |
| `ISelectionService` | `Abstractions/` | Current selection (incl. multi-select) |
| `IUndoService` / `IDesignerCommand` | `Abstractions/` | Safe edit history |
| `IPropertyService` | `Abstractions/` | Name/Value/geometry/font/image/hidden mutations |
| `IDocumentEditService` | `Abstractions/` | Insert/delete items and field bindings |
| `ISchemaService` | `Abstractions/` | Datasets, fields, parameters |
| `IDesignSurface` | `Abstractions/` | Interactive canvas contract |
| `IToolboxService` / `IReportItemFactory` | `Extensibility/` | Professional toolbox + insert factory |
| `IPropertyProvider` / `PropertyBrowserService` | `Properties/` | Selection-aware property categories |
| `DesignerContextMenuBuilder` | `ContextMenus/` | Right-click menus for surface/items |
| `RdlcDocument` / `ReportItemModel` | `Model/` | Lightweight DOM over `XElement` |
| `RdlUnits` | `Model/` | RDL size ↔ 96 DPI pixels |
| `RdlcPersistence` | `Persistence/` | Load/save implementation |
| Services | `Services/` | Selection, undo, properties, schema, edits |
| `RdlcVisualRenderer` / `DesignSurface` | `Surface/` | Visuals, hit-test, drag, drop, adorners |
| `RdlcLayoutDesignerView` / `PropertyBrowserPanel` | `Views/` | Shell UI |
| Icons | `Resources/Icons/` | Original Report Designer toolbox icons |

## UI layout

```
[ Toolbar: Undo/Redo · Delete · Expression ]
[ Toolbox | Data | Parameters ] [ Design surface ] [ Properties (provider-driven) ]
[ Status ]
```

## How to extend

1. Add a `ToolboxItemKind` + icon under `Resources/Icons/`, register it in `ToolboxService`.
2. Implement insert XML in `DocumentEditService` (or a custom `IReportItemFactory`).
3. Add an `IPropertyProvider` for kind-specific properties and register it in `PropertyBrowserService.CreateDefaultProviders()`.
4. Extend `DesignerContextMenuBuilder` for item-specific commands.
