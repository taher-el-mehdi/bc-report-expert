using ReportExpert.Core.Configuration;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Application.Reports.LoadReport;

public sealed class LoadReportRequest
{
    public required string FilePath { get; init; }
}

public sealed class LoadReportResult
{
    public required RdlcReportMetadata Metadata { get; init; }
}

public sealed class LoadReportUseCase
{
    private readonly IRdlcMetadataParser _metadataParser;

    public LoadReportUseCase(IRdlcMetadataParser metadataParser) => _metadataParser = metadataParser;

    public async Task<LoadReportResult> ExecuteAsync(LoadReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.FilePath))
            throw new FileNotFoundException("RDLC file not found.", request.FilePath);

        var metadata = await _metadataParser.ParseAsync(request.FilePath, cancellationToken);
        return new LoadReportResult { Metadata = metadata };
    }
}
