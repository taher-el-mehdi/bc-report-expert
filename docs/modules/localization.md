# Localization module

**Project:** `src/ReportExpert.Modules.Localization`  
**Status:** Present in the solution; **not yet hosted** in `ReportExpert.App` navigation.

Edit report labels and captions for multi-language BC reports.

## Features

- Load and edit `rd:ReportLabels` entries (`LabelName`, `Value`)
- Language layer selector (reads `Language` attribute on each label)
- Add/remove label rows
- **Export CSV** — `LabelName,Language,Value` for translators
- **Import CSV** — merge translated values back
- Save writes labels into the active `.rdlc` file

## Key classes

| Class | Role |
|-------|------|
| `LocalizationViewModel` | Grid editing, import/export commands |
| `LocalizationService` | XML read/write for `rd:ReportLabels` |
| `ReportLabelEntry` | One label row DTO |

## CSV format

```csv
LabelName,Language,Value
DocumentNo_Lbl,default,Document No.
DocumentNo_Lbl,fr-FR,N° document
```

## Wiring into the shell

To expose Localization in the app:

1. Add a `ProjectReference` from `ReportExpert.App` to this module
2. Add a nav item + content host in `MainWindow.xaml`
3. Optionally set report context from `ShellCoordinator` on `Preview.ReportLoaded`
