using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ReportExpert.Modules.Preview.Services;
using unvell.ReoGrid;
using unvell.ReoGrid.Events;
using unvell.ReoGrid.IO;
using WpfDataGrid = System.Windows.Controls.DataGrid;

namespace ReportExpert.App.Home;

/// <summary>
/// Owns Excel (ReoGrid + Open XML fallback) and Word (FlowDocument) hosts on Home.
/// </summary>
internal sealed class HomeOfficeDocumentController
{
    private readonly OfficeLayoutPreviewService _excelFallback = new();
    private readonly DocxFlowDocumentService _wordPreview = new();
    private readonly ReoGridControl _excelGrid;
    private readonly WpfDataGrid _excelFallbackGrid;
    private readonly FlowDocumentScrollViewer _wordPreviewViewer;
    private bool _useExcelFallback;
    private bool _suppressDirty;
    private Action? _onDirty;

    public HomeOfficeDocumentController(
        ReoGridControl excelGrid,
        WpfDataGrid excelFallbackGrid,
        FlowDocumentScrollViewer wordPreviewViewer)
    {
        _excelGrid = excelGrid;
        _excelFallbackGrid = excelFallbackGrid;
        _wordPreviewViewer = wordPreviewViewer;
    }

    public void SetDirtyHandler(Action? onDirty) => _onDirty = onDirty;

    public void LoadExcel(string path, Action<string> markLoaded)
    {
        _useExcelFallback = false;
        _excelFallbackGrid.Visibility = Visibility.Collapsed;
        _excelGrid.Visibility = Visibility.Visible;
        ClearWordPreview();

        try
        {
            _excelGrid.Reset();
            _excelGrid.Load(path, FileFormat.Excel2007);
            AttachExcelDirtyTracking(true);
            int sheets = _excelGrid.Worksheets.Count;
            markLoaded($"Excel · {sheets} sheet(s) · editable in ReoGrid");
        }
        catch (Exception ex)
        {
            AttachExcelDirtyTracking(false);
            _excelGrid.Visibility = Visibility.Collapsed;
            _excelFallbackGrid.Visibility = Visibility.Visible;
            _useExcelFallback = true;

            var preview = _excelFallback.Load(path);
            _excelFallbackGrid.ItemsSource = preview.SheetTable?.DefaultView;
            markLoaded(
                $"Excel · {preview.Summary} · read-only Open XML fallback ({ex.GetType().Name})");
        }
    }

    public void LoadWord(string path, Action<string> markLoaded)
    {
        AttachExcelDirtyTracking(false);
        (FlowDocument document, string summary) = _wordPreview.LoadWithSummary(path);
        _wordPreviewViewer.Document = document;
        markLoaded($"Word · {summary} · Open XML preview · edit in Microsoft Word");
    }

    public bool TrySaveExcel(string path, out string? errorMessage)
    {
        errorMessage = null;
        if (_useExcelFallback)
        {
            errorMessage = "Read-only Open XML fallback — open in Excel to edit.";
            return false;
        }

        _excelGrid.Save(path, FileFormat.Excel2007);
        return true;
    }

    public static string WordReadOnlyMessage =>
        "Word preview is read-only — use Open in Word to edit and save.";

    public void ClearWordPreview() =>
        _wordPreviewViewer.Document = new FlowDocument();

    public void RunWithDirtySuppressed(Action action)
    {
        try
        {
            _suppressDirty = true;
            action();
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    public static bool TryOpenExternally(string path, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private void AttachExcelDirtyTracking(bool attach)
    {
        foreach (var sheet in _excelGrid.Worksheets)
            sheet.CellDataChanged -= OnExcelCellDataChanged;

        if (!attach)
            return;

        foreach (var sheet in _excelGrid.Worksheets)
            sheet.CellDataChanged += OnExcelCellDataChanged;
    }

    private void OnExcelCellDataChanged(object? sender, CellEventArgs e)
    {
        if (!_suppressDirty)
            _onDirty?.Invoke();
    }
}
