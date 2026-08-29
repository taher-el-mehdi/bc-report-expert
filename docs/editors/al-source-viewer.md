# AL Source Viewer

The AL Source viewer is a read-only AvalonEdit panel that shows the paired `Report.al` file while previewing RDLC layouts.

## Where it appears

- **Preview:** resizable split panel beside the RDLC preview (toggle with toolbar **AL Source**; default visibility is a Settings option).

## Grammar updates

Official Microsoft AL TextMate grammar is vendored from [microsoft/AL `grammar/alsyntax.tmlanguage`](https://github.com/microsoft/AL/blob/master/grammar/alsyntax.tmlanguage).

To refresh the embedded grammar:

```powershell
./scripts/update-al-grammar.ps1
```

This downloads the latest plist grammar, converts it to JSON (`al.tmLanguage.json`), and updates files under `src/ReportExpert.Editors/Resources/Syntax/`.

## Architecture

| Component | Location |
|-----------|----------|
| Viewer control | `src/ReportExpert.Editors/Editors/AlSourceViewer.xaml` |
| ViewModel | `src/ReportExpert.Editors/ViewModels/AlSourceViewerViewModel.cs` |
| Syntax / theme services | `src/ReportExpert.Editors/Services/` |
| TextMate bridge (vendored) | `src/ReportExpert.Editors/TextMate/` |
| Core contracts | `src/ReportExpert.Core/Editors/` |
| App bootstrap | `src/ReportExpert.App/Shell/EditorServiceBootstrap.cs` |

AL paths are resolved from sibling `*.Report.al` files near the opened RDLC, then by searching the open workspace. When a workspace is open, `ShellCoordinator` sets the path resolver workspace root.
