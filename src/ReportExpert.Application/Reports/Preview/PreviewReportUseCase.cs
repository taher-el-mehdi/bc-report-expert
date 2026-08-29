using System.Data;
using ReportExpert.Core.Configuration;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Application.Reports.Preview;

public sealed class PreviewReportRequest
{
    public required string FilePath { get; init; }
    public required IReadOnlyList<DataTable> Tables { get; init; }
    public IDictionary<string, object?>? Parameters { get; init; }
}

public sealed class PreviewReportUseCase
{
    private readonly IReportPreviewService _previewService;
    private readonly IReportSession _reportSession;
    private readonly ISettingsService _settingsService;

    public PreviewReportUseCase(
        IReportPreviewService previewService,
        IReportSession reportSession,
        ISettingsService settingsService)
    {
        _previewService = previewService;
        _reportSession = reportSession;
        _settingsService = settingsService;
    }

    public Task ExecuteAsync(PreviewReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!_settingsService.Settings.AutoPreview)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            _previewService.LoadReport(request.FilePath, request.Tables, request.Parameters);
            _reportSession.SetParameters(request.Parameters?.ToDictionary(
                k => k.Key,
                v => v.Value,
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
        }, cancellationToken);
    }
}
