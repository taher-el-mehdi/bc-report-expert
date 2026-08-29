using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReportExpert.Common;
using ReportExpert.Core.Editors;
using ReportExpert.Editors.Editors;
using ReportExpert.Editors.ViewModels;
using ReportExpert.Modules.Preview.Export;
using ReportExpert.Modules.Preview.Generators;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.Modules.Preview.Views;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ReportExpert.Modules.Preview.ViewModels;

/// <summary>
/// View model for the Preview module: loads RDLC / Excel / Word layouts,
/// generates sample data for RDLC, drives the report viewer, and handles export/print.
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly RdlcMetadataParser _metadataParser = new();
    private readonly DummyDataGenerator _dataGenerator = new();
    private readonly ReportDataImporter _dataImporter = new();
    private readonly ReportExportService _exportService = new();
    private readonly SettingsService _settingsService;
    private readonly ISnackbarService _snackbarService;
    private readonly Func<Window?> _getOwnerWindow;

    private Dictionary<string, DataTable> _generatedTables = new(StringComparer.OrdinalIgnoreCase);
    private System.Timers.Timer? _parameterDebounceTimer;
    private string? _currentLayoutPath;

    /// <summary>Path of the layout currently shown in Preview (RDLC or Office).</summary>
    public string? CurrentLayoutPathForShell => _currentLayoutPath;

    public ReportPreviewService PreviewService { get; }

    /// <summary>Raised when Preview wants the shell to open a layout for editing on Home.</summary>
    public event Action<string>? EditOnHomeRequested;

    /// <summary>Raised after a layout has finished loading into Preview (RDLC or Office).</summary>
    public event Action<string>? ReportOpened;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _pageInfo = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private LayoutKind _currentLayoutKind = LayoutKind.None;

    [ObservableProperty]
    private string _officeExternalFileName = string.Empty;

    [ObservableProperty]
    private RdlcReportMetadata? _metadata;

    public bool IsRdlcLayout => CurrentLayoutKind == LayoutKind.Rdlc;
    public bool IsOfficeLayout => CurrentLayoutKind is LayoutKind.Excel or LayoutKind.Word;
    public bool IsExcelLayout => CurrentLayoutKind == LayoutKind.Excel;
    public bool IsWordLayout => CurrentLayoutKind == LayoutKind.Word;

    /// <summary>Sample rows per dataset — configured in Settings.</summary>
    public int RowCount => Math.Clamp(_settingsService.Settings.RowsGenerated, 1, 500);

    [ObservableProperty]
    private DataSourceMode _dataSourceMode = DataSourceMode.Dummy;

    [ObservableProperty]
    private string _dataSourceDisplay = "Dummy data";

    [ObservableProperty]
    private ExplorerTreeItem? _selectedExplorerItem;

    [ObservableProperty]
    private DataView? _selectedDataView;

    public ObservableCollection<ExplorerTreeItem> ExplorerItems { get; } = [];
    public ObservableCollection<ParameterEditorViewModel> ParameterEditors { get; } = [];

    public AlSourceViewerViewModel AlSource { get; }

    public PreviewViewModel(
        Func<Window?> ownerWindowAccessor,
        ReportPreviewService previewService,
        SettingsService settingsService,
        ISnackbarService snackbarService)
    {
        _getOwnerWindow = ownerWindowAccessor;
        PreviewService = previewService;
        _settingsService = settingsService;
        _snackbarService = snackbarService;
        AlSource = new AlSourceViewerViewModel(EditorServicesLocator.LoadService ?? new ReportExpert.Editors.Services.AlSourceLoadService())
        {
            IsPanelVisible = false
        };
    }

    partial void OnSelectedExplorerItemChanged(ExplorerTreeItem? value)
    {
        if (value?.DataSet is not null && _generatedTables.TryGetValue(value.DataSet.Name, out DataTable? table))
            SelectedDataView = table.DefaultView;
        else if (value?.Field is not null)
        {
            var parent = ExplorerItems.FirstOrDefault(ds =>
                ds.Children.Any(c => c.Name == value.Name));
            if (parent?.DataSet is not null &&
                _generatedTables.TryGetValue(parent.DataSet.Name, out DataTable? parentTable))
                SelectedDataView = parentTable.DefaultView;
        }
    }

    partial void OnCurrentLayoutKindChanged(LayoutKind value)
    {
        OnPropertyChanged(nameof(IsRdlcLayout));
        OnPropertyChanged(nameof(IsOfficeLayout));
        OnPropertyChanged(nameof(IsExcelLayout));
        OnPropertyChanged(nameof(IsWordLayout));
        OpenExternallyCommand.NotifyCanExecuteChanged();
        EditOnHomeCommand.NotifyCanExecuteChanged();
        GenerateDataCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenRdlcAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LayoutFileHelper.OpenDialogFilter,
            Title = "Open Report Layout"
        };

        if (dlg.ShowDialog() == true)
            await LoadReportAsync(dlg.FileName);
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentLayoutPath))
            return;

        await LoadReportAsync(_currentLayoutPath);
    }

    [RelayCommand(CanExecute = nameof(IsOfficeLayout))]
    private void OpenExternally()
    {
        if (string.IsNullOrWhiteSpace(_currentLayoutPath) || !File.Exists(_currentLayoutPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentLayoutPath,
                UseShellExecute = true
            });
            StatusMessage = $"Opened {Path.GetFileName(_currentLayoutPath)} externally";
        }
        catch (Exception ex)
        {
            ShowSnackbar("Open failed", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand(CanExecute = nameof(IsOfficeLayout))]
    private void EditOnHome()
    {
        if (string.IsNullOrWhiteSpace(_currentLayoutPath))
            return;

        EditOnHomeRequested?.Invoke(_currentLayoutPath);
    }

    [RelayCommand(CanExecute = nameof(IsRdlcLayout))]
    private async Task GenerateDataAsync()
    {
        if (Metadata is null || !IsRdlcLayout)
            return;

        await RunBusyAsync("Regenerating sample data...", async () =>
        {
            await Task.Run(GenerateDataInternal);
            if (_settingsService.Settings.AutoPreview)
                RebindRdlcPreview();

            BuildExplorerTree();
            StatusMessage = $"Generated {RowCount} rows per dataset.";
        });
    }

    [RelayCommand]
    private void RefreshPreview()
    {
        if (!IsRdlcLayout)
        {
            StatusMessage = "In-app data preview is only available for RDLC. Use Open Externally for Excel/Word.";
            return;
        }

        try
        {
            RebindRdlcPreview();
            StatusMessage = "Preview refreshed.";
        }
        catch (Exception ex)
        {
            ShowSnackbar("Preview refresh failed", ex.InnerException?.Message ?? ex.Message, ControlAppearance.Danger);
            StatusMessage = $"Preview failed: {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportAsync(object? formatParameter)
    {
        if (!IsRdlcLayout)
            return;

        ExportFormat format = formatParameter switch
        {
            ExportFormat ef => ef,
            string s when Enum.TryParse<ExportFormat>(s, true, out var parsed) => parsed,
            _ => ExportFormat.Pdf
        };
        await ExportInternalAsync(format);
    }

    [RelayCommand]
    private void Print()
    {
        if (IsRdlcLayout)
            PreviewService.Print();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (IsRdlcLayout)
            PreviewService.PreviousPage();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (IsRdlcLayout)
            PreviewService.NextPage();
    }

    [RelayCommand]
    private void LastPage()
    {
        if (IsRdlcLayout)
            PreviewService.LastPage();
    }

    [RelayCommand]
    private void CopySchema()
    {
        var dataset = GetSelectedDataSet();
        if (dataset is null) return;
        System.Windows.Clipboard.SetText(DummyDataGenerator.BuildSchemaText(dataset));
        ShowSnackbar("Schema copied", "Dataset schema copied to clipboard.", ControlAppearance.Success);
    }

    [RelayCommand]
    private async Task ImportDataAsync()
    {
        var dataset = GetSelectedDataSet() ?? Metadata?.DataSets.FirstOrDefault();
        if (dataset is null)
        {
            ShowSnackbar("No dataset", "Load a report with datasets first.", ControlAppearance.Caution);
            return;
        }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Data files|*.json;*.xml;*.csv|JSON|*.json|XML|*.xml|CSV|*.csv",
            Title = $"Import data for {dataset.Name}"
        };

        if (dlg.ShowDialog() != true)
            return;

        await RunBusyAsync("Importing data...", async () =>
        {
            DataTable table = await Task.Run(() => _dataImporter.Import(dlg.FileName, dataset));
            _generatedTables[dataset.Name] = table;
            DataSourceMode = DataSourceMode.Imported;
            DataSourceDisplay = $"Imported: {Path.GetFileName(dlg.FileName)} ({table.Rows.Count} rows)";

            if (_settingsService.Settings.AutoPreview)
                PreviewService.LoadReport(Metadata!.FilePath, _generatedTables.Values.ToList(), BuildParameterDictionary());

            BuildExplorerTree();
            StatusMessage = $"Imported {table.Rows.Count} rows into {dataset.Name}.";
            await Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task UseDummyDataAsync()
    {
        if (Metadata is null || !IsRdlcLayout) return;

        await RunBusyAsync("Generating sample data...", async () =>
        {
            await Task.Run(GenerateDataInternal);
            DataSourceMode = DataSourceMode.Dummy;
            DataSourceDisplay = $"Dummy data ({RowCount} rows/dataset)";

            if (_settingsService.Settings.AutoPreview)
                RebindRdlcPreview();

            BuildExplorerTree();
            StatusMessage = "Using generated dummy data.";
            await Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task ExportDataJsonAsync() => await ExportGeneratedDataAsync("json");

    [RelayCommand]
    private async Task ExportDataCsvAsync() => await ExportGeneratedDataAsync("csv");

    [RelayCommand]
    private async Task ExportDataXmlAsync() => await ExportGeneratedDataAsync("xml");

    public void ApplySavedSettings()
    {
        OnPropertyChanged(nameof(RowCount));
        AlSource.IsPanelVisible = false;
        ApplyTheme();
    }

    public async Task LoadReportFromDropAsync(string path) => await LoadReportAsync(path);

    /// <summary>
    /// Loads an RDLC, Excel, or Word layout into Preview.
    /// </summary>
    public async Task LoadReportAsync(string layoutPath)
    {
        if (!File.Exists(layoutPath))
        {
            await DialogService.ShowErrorAsync(_getOwnerWindow(), "File not found",
                $"The file could not be found:\n{layoutPath}");
            return;
        }

        LayoutKind kind = LayoutFileHelper.DetectKind(layoutPath);
        if (kind == LayoutKind.None)
        {
            await DialogService.ShowErrorAsync(_getOwnerWindow(), "Unsupported layout",
                "Preview supports .rdlc, .rdl, .xlsx, and .docx layout files.");
            return;
        }

        if (kind == LayoutKind.Rdlc)
            await LoadRdlcAsync(layoutPath);
        else
            await LoadOfficeLayoutAsync(layoutPath, kind);
    }

    private async Task LoadRdlcAsync(string rdlcPath)
    {
        await RunBusyAsync("Loading report...", async () =>
        {
            ClearOfficeExternal();
            _currentLayoutPath = rdlcPath;

            // Match working Preview pipeline: parse → sibling JSON (if present) or dummy → LoadReport.
            Metadata = await _metadataParser.ParseAsync(rdlcPath);
            BuildParameterEditors();
            await Task.Run(GenerateDataInternal);

            CurrentLayoutKind = LayoutKind.Rdlc;
            HasReport = true;

            if (_settingsService.Settings.AutoPreview)
            {
                PreviewService.LoadReport(
                    rdlcPath,
                    _generatedTables.Values.ToList(),
                    BuildParameterDictionary());
            }

            BuildExplorerTree();
            UpdatePageInfo();
            StatusMessage = DataSourceMode == DataSourceMode.Imported
                ? $"Loaded {Metadata.ReportName} · {DataSourceDisplay}"
                : $"Loaded {Metadata.ReportName}";

            await LoadAlSourceForReportAsync(rdlcPath);
            ReportOpened?.Invoke(rdlcPath);
        });
    }

    /// <summary>Rebinds ReportViewer with the current dummy/imported tables (not stale Refresh cache).</summary>
    private void RebindRdlcPreview()
    {
        if (Metadata is null || !IsRdlcLayout)
            return;

        PreviewService.LoadReport(
            Metadata.FilePath,
            _generatedTables.Values.ToList(),
            BuildParameterDictionary());
        UpdatePageInfo();
    }

    private async Task LoadOfficeLayoutAsync(string path, LayoutKind kind)
    {
        string label = kind == LayoutKind.Excel ? "Excel" : "Word";
        await RunBusyAsync($"Opening {label} layout…", async () =>
        {
            ClearRdlcPreview();
            _currentLayoutPath = path;
            OfficeExternalFileName = Path.GetFileName(path);
            CurrentLayoutKind = kind;
            HasReport = true;
            PageInfo = $"Opened in {label}";

            // Real-world preview: open the layout in Microsoft Excel / Word.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                StatusMessage = $"Opened {OfficeExternalFileName} in {label} · edit layouts on Home";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not open {label}: {ex.Message}";
                ShowSnackbar($"Open {label} failed", ex.Message, ControlAppearance.Caution);
            }

            await LoadAlSourceForReportAsync(path);
            ReportOpened?.Invoke(path);
            await Task.CompletedTask;
        });
    }

    private void ClearOfficeExternal()
    {
        OfficeExternalFileName = string.Empty;
    }

    private void ClearRdlcPreview()
    {
        Metadata = null;
        ExplorerItems.Clear();
        ParameterEditors.Clear();
        _generatedTables.Clear();
        SelectedDataView = null;
        DataSourceDisplay = string.Empty;
        PageInfo = string.Empty;
    }

    public async Task LoadAlSourceForReportAsync(string layoutPath)
    {
        string? alPath = EditorServicesLocator.PathResolver?.ResolveFromRdlc(layoutPath);
        await AlSource.LoadAsync(alPath);
        AlSource.IsPanelVisible = false;
    }

    public async Task LoadAlSourceAsync(string? alPath)
    {
        await AlSource.LoadAsync(alPath);
        AlSource.IsPanelVisible = false;
    }

    public void OnParameterChanged()
    {
        if (!IsRdlcLayout)
            return;

        _parameterDebounceTimer?.Stop();
        _parameterDebounceTimer?.Dispose();
        _parameterDebounceTimer = new System.Timers.Timer(300) { AutoReset = false };
        _parameterDebounceTimer.Elapsed += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!IsRdlcLayout)
                    return;
                PreviewService.SetParameters(BuildParameterDictionary());
                UpdatePageInfo();
            });
        };
        _parameterDebounceTimer.Start();
    }

    private void GenerateDataInternal()
    {
        if (Metadata is null) return;

        string? sidecar = ReportDataImporter.ResolveSiblingJsonPath(Metadata.FilePath);
        if (sidecar is not null)
        {
            try
            {
                var imported = _dataImporter.ImportAllFromJson(sidecar, Metadata.DataSets);
                if (imported.Count > 0)
                {
                    _generatedTables = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
                    foreach (var ds in Metadata.DataSets)
                    {
                        if (imported.TryGetValue(ds.Name, out DataTable? table))
                            _generatedTables[ds.Name] = table;
                        else
                            _generatedTables[ds.Name] = _dataGenerator.GenerateTable(ds, RowCount);
                    }

                    DataSourceMode = DataSourceMode.Imported;
                    DataSourceDisplay = Path.GetFileName(sidecar);
                    return;
                }
            }
            catch
            {
                // Fall through to generated dummy data.
            }
        }

        var tables = _dataGenerator.GenerateAll(Metadata.DataSets, RowCount);
        _generatedTables = tables.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);
        DataSourceMode = DataSourceMode.Dummy;
        DataSourceDisplay = $"Dummy data ({RowCount} rows/dataset)";
    }

    private void BuildExplorerTree()
    {
        ExplorerItems.Clear();
        if (Metadata is null) return;

        foreach (var dataset in Metadata.DataSets)
        {
            _generatedTables.TryGetValue(dataset.Name, out DataTable? table);
            var children = dataset.Fields.Select(field =>
            {
                string? sample = null;
                if (table is not null && table.Rows.Count > 0 && table.Columns.Contains(field.DataField))
                {
                    object val = table.Rows[0][field.DataField];
                    sample = val == DBNull.Value ? null : Convert.ToString(val);
                }

                return new ExplorerTreeItem
                {
                    Name = field.Name,
                    Kind = "Field",
                    TypeName = field.TypeName,
                    SampleValue = sample,
                    Field = field,
                    DataSet = dataset
                };
            }).ToList();

            ExplorerItems.Add(new ExplorerTreeItem
            {
                Name = dataset.Name,
                Kind = "DataSet",
                DataSet = dataset,
                Children = children
            });
        }
    }

    private void BuildParameterEditors()
    {
        ParameterEditors.Clear();
        if (Metadata is null) return;

        foreach (var param in Metadata.Parameters)
        {
            var editor = new ParameterEditorViewModel(param);
            editor.PropertyChanged += (_, _) => OnParameterChanged();
            ParameterEditors.Add(editor);
        }
    }

    private Dictionary<string, object?> BuildParameterDictionary() =>
        ParameterEditors.ToDictionary(e => e.Info.Name, e => e.GetValue(), StringComparer.OrdinalIgnoreCase);

    private RdlcDataSetInfo? GetSelectedDataSet()
    {
        if (SelectedExplorerItem?.DataSet is not null)
            return SelectedExplorerItem.DataSet;

        if (SelectedExplorerItem?.Field is not null)
            return Metadata?.DataSets.FirstOrDefault(ds =>
                ds.Fields.Any(f => f.Name == SelectedExplorerItem.Field.Name));

        return Metadata?.DataSets.FirstOrDefault();
    }

    private DataTable? GetSelectedDataTable()
    {
        var dataset = GetSelectedDataSet();
        if (dataset is null) return null;
        _generatedTables.TryGetValue(dataset.Name, out DataTable? table);
        return table;
    }

    private async Task ExportInternalAsync(ExportFormat format)
    {
        if (Metadata is null || !IsRdlcLayout)
            return;

        string defaultName = Metadata.ReportName + format.DefaultExtension();
        string initialDir = string.IsNullOrWhiteSpace(_settingsService.Settings.DefaultExportFolder)
            ? Path.GetDirectoryName(Metadata.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _settingsService.Settings.DefaultExportFolder;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = $"{format.DisplayName()}|*{format.DefaultExtension()}",
            FileName = defaultName,
            InitialDirectory = initialDir
        };

        if (dlg.ShowDialog() != true)
            return;

        await RunBusyAsync($"Exporting {format.DisplayName()}...", async () =>
        {
            await _exportService.ExportAsync(
                Metadata.FilePath,
                _generatedTables.Values.ToList(),
                BuildParameterDictionary(),
                format,
                dlg.FileName);

            ShowSnackbar("Export complete", $"Exported to {Path.GetFileName(dlg.FileName)}", ControlAppearance.Success);
            StatusMessage = $"Exported {format.DisplayName()} successfully.";
        });
    }

    private async Task ExportGeneratedDataAsync(string format)
    {
        var table = GetSelectedDataTable();
        if (table is null)
        {
            ShowSnackbar("No dataset", "Select a dataset first.", ControlAppearance.Caution);
            return;
        }

        string ext = format switch
        {
            "json" => ".json",
            "csv" => ".csv",
            _ => ".xml"
        };
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = format switch
            {
                "json" => "JSON|*.json",
                "csv" => "CSV|*.csv",
                _ => "XML|*.xml"
            },
            FileName = table.TableName + ext
        };

        if (dlg.ShowDialog() != true)
            return;

        string content = format switch
        {
            "json" => DummyDataGenerator.ExportTableToJson(table),
            "csv" => DummyDataGenerator.ExportTableToCsv(table),
            _ => DummyDataGenerator.ExportTableToXml(table)
        };

        await File.WriteAllTextAsync(dlg.FileName, content);
        ShowSnackbar("Export complete", $"Data exported to {Path.GetFileName(dlg.FileName)}", ControlAppearance.Success);
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        IsBusy = true;
        StatusMessage = message;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await DialogService.ShowErrorAsync(
                _getOwnerWindow(),
                "Operation failed",
                ex.InnerException?.Message ?? ex.Message,
                exception: ex);
            StatusMessage = "Error";
        }
        finally
        {
            IsBusy = false;
            UpdatePageInfo();
        }
    }

    public void UpdatePageInfo()
    {
        if (IsOfficeLayout)
        {
            PageInfo = string.IsNullOrWhiteSpace(OfficeExternalFileName)
                ? "External"
                : $"External · {OfficeExternalFileName}";
            return;
        }

        int current = PreviewService.CurrentPage;
        int total = PreviewService.TotalPages;
        PageInfo = total > 0 ? $"Page {current} of {total}" : string.Empty;
    }

    private void ShowSnackbar(string title, string message, ControlAppearance appearance) =>
        _snackbarService.Show(title, message, appearance, null, TimeSpan.FromSeconds(3));

    public void ApplyTheme()
    {
        string theme = _settingsService.Settings.Theme;
        ApplicationTheme appTheme = theme switch
        {
            ThemeNames.Dark => ApplicationTheme.Dark,
            ThemeNames.Light => ApplicationTheme.Light,
            _ => ApplicationThemeManager.GetAppTheme()
        };

        if (theme == ThemeNames.System)
            ApplicationThemeManager.ApplySystemTheme();
        else
            ApplicationThemeManager.Apply(appTheme, WindowBackdropType.Mica);

        EditorServicesLocator.ThemeService?.NotifyThemeChanged();
        EditorServicesLocator.SyntaxHighlighting?.RefreshAllThemes();
    }
}
