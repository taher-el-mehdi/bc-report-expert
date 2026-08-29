using System.Data;
using System.IO;
using Microsoft.Reporting.WinForms;
using ReportParameter = Microsoft.Reporting.WinForms.ReportParameter;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Wraps the WinForms <see cref="ReportViewer"/> control for local RDLC rendering.
/// Binds in-memory <see cref="DataTable"/> sources and report parameters, and exposes zoom/page helpers.
/// </summary>
public sealed class ReportPreviewService : IDisposable
{
    private ReportViewer? _viewer;
    private string? _currentRdlcPath;
    private IReadOnlyList<DataTable> _tables = [];
    private Dictionary<string, object?> _parameters = new(StringComparer.OrdinalIgnoreCase);

    public ReportViewer Viewer
    {
        get
        {
            if (_viewer is null)
            {
                _viewer = new ReportViewer
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ProcessingMode = ProcessingMode.Local,
                    ShowExportButton = false,
                    ShowPrintButton = true,
                    ShowRefreshButton = true,
                    ShowFindControls = true,
                    ShowPageNavigationControls = true,
                    ShowZoomControl = true,
                    ShowToolBar = true,
                    ShowDocumentMapButton = true,
                    ShowParameterPrompts = false,
                    PromptAreaCollapsed = true
                };
            }

            return _viewer;
        }
    }

    public int CurrentPage => _viewer?.CurrentPage ?? 0;
    public int TotalPages => _viewer?.GetTotalPages() ?? 0;

    /// <summary>Loads an RDLC definition and binds datasets/parameters, then refreshes the viewer.</summary>
    public void LoadReport(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?>? parameters = null)
    {
        _currentRdlcPath = rdlcPath;
        _tables = tables;
        _parameters = parameters?.ToDictionary(
            k => k.Key,
            v => v.Value,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var localReport = Viewer.LocalReport;
        localReport.DataSources.Clear();
        localReport.ReportPath = string.Empty;

        byte[] definition = File.ReadAllBytes(rdlcPath);
        using var stream = new MemoryStream(definition);
        localReport.EnableHyperlinks = true;
        localReport.EnableExternalImages = true;
        localReport.LoadReportDefinition(stream);
        // Re-apply after load — some RDLC versions read the flag only once processing starts.
        localReport.EnableHyperlinks = true;
        localReport.EnableExternalImages = true;

        foreach (var table in tables)
            localReport.DataSources.Add(new ReportDataSource(table.TableName, table));

        ApplyParameters(localReport);
        Viewer.RefreshReport();
    }

    public void SetParameters(IDictionary<string, object?> parameters)
    {
        _parameters = parameters.ToDictionary(
            k => k.Key,
            v => v.Value,
            StringComparer.OrdinalIgnoreCase);

        if (_viewer is not null)
        {
            ApplyParameters(_viewer.LocalReport);
            _viewer.RefreshReport();
        }
    }

    public void Refresh()
    {
        if (_viewer is null || string.IsNullOrEmpty(_currentRdlcPath))
            return;

        LoadReport(_currentRdlcPath, _tables, _parameters);
    }

    public void Print() => _viewer?.PrintDialog();

    public void ZoomIn()
    {
        if (_viewer is null) return;
        _viewer.ZoomMode = ZoomMode.Percent;
        _viewer.ZoomPercent = Math.Min(500, _viewer.ZoomPercent + 25);
    }

    public void ZoomOut()
    {
        if (_viewer is null) return;
        _viewer.ZoomMode = ZoomMode.Percent;
        _viewer.ZoomPercent = Math.Max(10, _viewer.ZoomPercent - 25);
    }

    public void FitWidth()
    {
        if (_viewer is null) return;
        _viewer.ZoomMode = ZoomMode.PageWidth;
    }

    public void FitPage()
    {
        if (_viewer is null) return;
        _viewer.ZoomMode = ZoomMode.FullPage;
    }

    public void FirstPage()
    {
        if (_viewer is null) return;
        _viewer.CurrentPage = 1;
    }

    public void PreviousPage()
    {
        if (_viewer is null) return;
        _viewer.CurrentPage = Math.Max(1, _viewer.CurrentPage - 1);
    }

    public void NextPage()
    {
        if (_viewer is null) return;
        int total = _viewer.GetTotalPages();
        _viewer.CurrentPage = Math.Min(total, _viewer.CurrentPage + 1);
    }

    public void LastPage()
    {
        if (_viewer is null) return;
        _viewer.CurrentPage = _viewer.GetTotalPages();
    }

    private void ApplyParameters(LocalReport localReport)
    {
        var reportParams = new List<ReportParameter>();
        var defined = localReport.GetParameters().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in _parameters)
        {
            if (!defined.ContainsKey(name))
                continue;

            reportParams.Add(new ReportParameter(name, value?.ToString() ?? string.Empty));
        }

        if (reportParams.Count > 0)
            localReport.SetParameters(reportParams);
    }

    public void Dispose()
    {
        _viewer?.Dispose();
        _viewer = null;
    }
}
