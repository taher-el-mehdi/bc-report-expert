using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Reporting;

public interface IRdlcMetadataParser
{
    RdlcReportMetadata? CachedMetadata { get; }
    Task<RdlcReportMetadata> ParseAsync(string rdlcPath, CancellationToken cancellationToken = default);
    RdlcReportMetadata Parse(string rdlcPath);
    void InvalidateCache();
}
