using System.Data;
using ReportExpert.Core.Configuration;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Export;
using ReportExpert.Domain.Models;

namespace ReportExpert.Application.Reports.Export;

public sealed class ExportReportRequest
{
    public required string FilePath { get; init; }
    public required RdlcReportMetadata Metadata { get; init; }
    public required IReadOnlyDictionary<string, DataTable> Tables { get; init; }
    public required IDictionary<string, object?> Parameters { get; init; }
    public required ExportFormat Format { get; init; }
    public required string DestinationPath { get; init; }
}

public sealed class ExportReportUseCase
{
    private readonly IReportExportService _exportService;

    public ExportReportUseCase(IReportExportService exportService) => _exportService = exportService;

    public Task ExecuteAsync(ExportReportRequest request, CancellationToken cancellationToken = default) =>
        _exportService.ExportAsync(
            request.FilePath,
            request.Tables.Values.ToList(),
            request.Parameters,
            request.Format,
            request.DestinationPath,
            cancellationToken);
}
