using System.Data;
using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Reporting;

public interface IReportSession
{
    string? FilePath { get; }
    RdlcReportMetadata? Metadata { get; }
    IReadOnlyDictionary<string, DataTable> GeneratedTables { get; }
    IReadOnlyDictionary<string, object?> Parameters { get; }
    void SetReport(string filePath, RdlcReportMetadata metadata);
    void SetGeneratedTables(IReadOnlyDictionary<string, DataTable> tables);
    void SetParameters(IReadOnlyDictionary<string, object?> parameters);
    void Clear();
}
