using System.Windows.Forms.Integration;
using Microsoft.Reporting.WinForms;
using ReportExpert.Modules.Preview.Services;

namespace ReportExpert.Modules.Preview.Rendering;

public sealed class ReportViewerHost : System.Windows.Controls.UserControl, IDisposable
{
    private readonly WindowsFormsHost _host;
    private readonly ReportPreviewService _previewService;

    public ReportPreviewService PreviewService => _previewService;

    public ReportViewerHost(ReportPreviewService previewService)
    {
        _previewService = previewService;
        _host = new WindowsFormsHost
        {
            Child = _previewService.Viewer
        };

        Content = _host;
    }

    public void Dispose()
    {
        _host.Dispose();
        _previewService.Dispose();
    }
}
