using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.Common;
using ReportExpert.Modules.Preview.Generators;
using ReportExpert.Modules.Preview.Models;
using ReportExpert.Modules.Preview.Services;

namespace ReportExpert.Modules.Preview.ViewModels;

/// <summary>
/// Auto-generates an RDLC PDF preview with sample data (or same-name .json sidecar) and supports saving a copy.
/// Sample row count comes from application Settings.
/// </summary>
public sealed partial class RdlcPreviewDialogViewModel : ObservableObject
{
    private readonly RdlcMetadataParser _parser = new();
    private readonly DummyDataGenerator _generator = new();
    private readonly ReportDataImporter _importer = new();
    private readonly SettingsService _settings;

    private string? _sidecarJsonPath;
    private bool _usedSidecarData;

    public ObservableCollection<DatasetConfigItem> Datasets { get; } = [];
    public ObservableCollection<ParameterEditorViewModel> Parameters { get; } = [];

    /// <summary>Raised when a PDF was written and should be shown in the WebView2 host.</summary>
    public event Action<string>? PdfReady;

    [ObservableProperty]
    private string _reportTitle = string.Empty;

    [ObservableProperty]
    private string _reportPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Preparing preview…";

    [ObservableProperty]
    private string _reportDetails = string.Empty;

    [ObservableProperty]
    private string _generationSummary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasParameters => Parameters.Count > 0;
    public bool CanDownload => HasPreview && !IsBusy && File.Exists(OutputPath);
    public bool CanRefresh => !IsBusy && !string.IsNullOrWhiteSpace(ReportPath) && File.Exists(ReportPath);

    public int RowCount => Math.Clamp(_settings.Settings.RowsGenerated, 1, 500);

    public RdlcReportMetadata? Metadata { get; private set; }

    public RdlcPreviewDialogViewModel(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task InitializeAsync(string rdlcPath)
    {
        ReportPath = rdlcPath;
        ReportTitle = Path.GetFileName(rdlcPath);
        ErrorMessage = null;
        HasPreview = false;
        GenerationSummary = string.Empty;
        _sidecarJsonPath = ReportDataImporter.ResolveSiblingJsonPath(rdlcPath);
        _usedSidecarData = false;
        OutputPath = BuildTempPdfPath(rdlcPath);
        StatusMessage = "Loading report…";

        IsBusy = true;
        try
        {
            Metadata = await _parser.ParseAsync(rdlcPath);
            ReportTitle = Metadata.ReportName;
            ReportDetails = BuildReportDetails(Metadata, _sidecarJsonPath);

            int rows = RowCount;
            Datasets.Clear();
            foreach (var ds in Metadata.DataSets)
            {
                Datasets.Add(new DatasetConfigItem
                {
                    Name = ds.Name,
                    FieldCount = ds.Fields.Count,
                    RowCount = rows
                });
            }

            Parameters.Clear();
            foreach (var p in Metadata.Parameters)
                Parameters.Add(new ParameterEditorViewModel(p));
            OnPropertyChanged(nameof(HasParameters));
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatException(ex);
            StatusMessage = "Failed to parse report.";
            ReportDetails = string.Empty;
            IsBusy = false;
            NotifyCommands();
            return;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }

        await GeneratePdfAsync();
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value) => NotifyCommands();

    partial void OnHasPreviewChanged(bool value) => NotifyCommands();

    partial void OnOutputPathChanged(string value) => NotifyCommands();

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task GeneratePdfAsync()
    {
        if (string.IsNullOrWhiteSpace(ReportPath) || !File.Exists(ReportPath))
        {
            ErrorMessage = "RDLC file was not found.";
            return;
        }

        if (Metadata is null)
        {
            ErrorMessage = "Report metadata is not loaded.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
            OutputPath = BuildTempPdfPath(ReportPath);

        int rows = RowCount;
        foreach (var ds in Datasets)
            ds.RowCount = rows;

        await RunBusyAsync("Creating PDF preview…", async () =>
        {
            var (tables, sidecarWarning) = await Task.Run(BuildDataTables);
            var parameters = BuildParameters();

            string? renderError = await Task.Run(() =>
                BcXmlReportRenderer.TryRenderTablesToPdf(ReportPath, tables, OutputPath, parameters));

            if (renderError is not null)
            {
                HasPreview = false;
                ErrorMessage = renderError;
                StatusMessage = "PDF preview failed.";
                return;
            }

            HasPreview = true;
            ErrorMessage = null;
            UpdateGenerationSummary(tables, sidecarWarning);
            StatusMessage = _usedSidecarData && _sidecarJsonPath is not null
                ? $"Preview ready · {Path.GetFileName(_sidecarJsonPath)} · {rows} rows"
                : $"Preview ready · sample data · {rows} rows";
            PdfReady?.Invoke(OutputPath);
        });
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void DownloadPdf()
    {
        if (!File.Exists(OutputPath))
        {
            ErrorMessage = "No PDF is available to download yet.";
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF files|*.pdf",
            Title = "Save PDF as",
            FileName = Path.GetFileNameWithoutExtension(ReportPath) + ".pdf",
            InitialDirectory = PreferDirectory(
                _settings.Settings.DefaultExportFolder,
                ReportPath,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            string? dir = Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(OutputPath, dlg.FileName, overwrite: true);
            StatusMessage = $"Saved · {Path.GetFileName(dlg.FileName)}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dlg.FileName,
                    UseShellExecute = true
                });
            }
            catch
            {
                // File was saved; opening is optional.
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatException(ex);
            StatusMessage = "Save failed.";
        }
    }

    private (List<DataTable> Tables, string? SidecarWarning) BuildDataTables()
    {
        string? sidecarWarning = null;
        var tables = new List<DataTable>();
        if (Metadata is null)
            return (tables, null);

        _usedSidecarData = false;
        Dictionary<string, DataTable>? fromJson = null;
        int rows = RowCount;

        if (_sidecarJsonPath is not null)
        {
            try
            {
                fromJson = _importer.ImportAllFromJson(_sidecarJsonPath, Metadata.DataSets);
                if (fromJson.Count > 0)
                    _usedSidecarData = true;
                else
                    sidecarWarning = $"JSON sidecar had no matching datasets ({Path.GetFileName(_sidecarJsonPath)}).";
            }
            catch (Exception ex)
            {
                sidecarWarning = $"JSON sidecar failed ({Path.GetFileName(_sidecarJsonPath)}): {ex.Message}";
                fromJson = null;
            }
        }

        foreach (var ds in Metadata.DataSets)
        {
            var cfg = Datasets.FirstOrDefault(d =>
                string.Equals(d.Name, ds.Name, StringComparison.OrdinalIgnoreCase));

            DataTable table;
            if (fromJson is not null &&
                fromJson.TryGetValue(ds.Name, out DataTable? imported))
            {
                table = LimitRows(imported, rows);
            }
            else
            {
                table = _generator.GenerateTable(ds, rows);
            }

            tables.Add(table);
            if (cfg is not null)
                cfg.GeneratedRows = table.Rows.Count;
        }

        return (tables, sidecarWarning);
    }

    private static DataTable LimitRows(DataTable source, int maxRows)
    {
        if (source.Rows.Count <= maxRows)
            return source;

        var limited = source.Clone();
        for (int i = 0; i < maxRows; i++)
            limited.ImportRow(source.Rows[i]);
        return limited;
    }

    private Dictionary<string, object?> BuildParameters() =>
        Parameters.ToDictionary(p => p.Info.Name, p => p.GetValue(), StringComparer.OrdinalIgnoreCase);

    private void UpdateGenerationSummary(IReadOnlyList<DataTable> tables, string? sidecarWarning)
    {
        var sb = new StringBuilder();
        int totalRows = tables.Sum(t => t.Rows.Count);
        if (_usedSidecarData && _sidecarJsonPath is not null)
            sb.Append($"JSON · {Path.GetFileName(_sidecarJsonPath)} · {tables.Count} dataset(s) · {totalRows:N0} row(s)");
        else
            sb.Append($"Sample data · {tables.Count} dataset(s) · {totalRows:N0} row(s)");
        foreach (DataTable table in tables)
            sb.AppendLine().Append($"  · {table.TableName}: {table.Rows.Count:N0} rows · {table.Columns.Count} fields");
        if (Parameters.Count > 0)
            sb.AppendLine().Append($"  · {Parameters.Count} parameter(s)");
        if (!string.IsNullOrWhiteSpace(sidecarWarning))
            sb.AppendLine().Append($"  · {sidecarWarning}");
        GenerationSummary = sb.ToString().TrimEnd();
    }

    private static string BuildTempPdfPath(string rdlcPath)
    {
        string dir = AppConstants.GetTempDirectory(AppConstants.TempPreviewFolderName);
        Directory.CreateDirectory(dir);
        string name = Path.GetFileNameWithoutExtension(rdlcPath);
        if (string.IsNullOrWhiteSpace(name))
            name = "report";
        return Path.Combine(dir, name + ".pdf");
    }

    private static string BuildReportDetails(RdlcReportMetadata meta, string? sidecarJsonPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(meta.PageSizeDisplay) &&
            !string.Equals(meta.PageSizeDisplay, "Unknown", StringComparison.OrdinalIgnoreCase))
            parts.Add($"Page size: {meta.PageSizeDisplay}");
        if (meta.DataSets.Count > 0)
            parts.Add($"{meta.DataSets.Count} dataset(s)");
        if (meta.Parameters.Count > 0)
            parts.Add($"{meta.Parameters.Count} parameter(s)");
        if (sidecarJsonPath is not null)
            parts.Add($"data: {Path.GetFileName(sidecarJsonPath)}");
        return string.Join(" · ", parts);
    }

    private static string PreferDirectory(params string?[] paths)
    {
        foreach (string? path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (Directory.Exists(path))
                return path;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanRefresh));
        GeneratePdfCommand.NotifyCanExecuteChanged();
        DownloadPdfCommand.NotifyCanExecuteChanged();
    }

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = message;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = FormatException(ex);
            StatusMessage = "PDF preview failed.";
            HasPreview = false;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private static string FormatException(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message) &&
                (parts.Count == 0 || !parts[^1].Equals(cur.Message, StringComparison.Ordinal)))
                parts.Add(cur.Message);
        }

        return string.Join(" → ", parts);
    }
}

public sealed partial class DatasetConfigItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _fieldCount;

    [ObservableProperty]
    private int _rowCount = 20;

    [ObservableProperty]
    private int _generatedRows;
}
