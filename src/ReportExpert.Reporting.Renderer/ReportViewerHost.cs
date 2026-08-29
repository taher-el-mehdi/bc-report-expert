using System.Windows.Forms.Integration;
using ReportExpert.Core.Reporting;
using Microsoft.Reporting.WinForms;

namespace ReportExpert.Reporting.Renderer;

public sealed class ReportViewerHost : System.Windows.Controls.UserControl, IDisposable
{
    private readonly WindowsFormsHost _host;
    private readonly IReportPreviewService _previewService;

    public IReportPreviewService PreviewService => _previewService;

    public ReportViewerHost(IReportPreviewService previewService)
    {
        _previewService = previewService;
        _host = new WindowsFormsHost
        {
            Child = (ReportViewer)previewService.Viewer
        };

        Content = _host;
    }

    public void Dispose()
    {
        _host.Dispose();
        _previewService.Dispose();
    }
}
