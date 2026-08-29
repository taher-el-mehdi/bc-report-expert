using System.Data;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Reporting.Renderer;

public sealed class ReportSession : IReportSession
{
    public string? FilePath { get; private set; }
    public RdlcReportMetadata? Metadata { get; private set; }
    public IReadOnlyDictionary<string, DataTable> GeneratedTables { get; private set; } =
        new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, object?> Parameters { get; private set; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public void SetReport(string filePath, RdlcReportMetadata metadata)
    {
        FilePath = filePath;
        Metadata = metadata;
    }

    public void SetGeneratedTables(IReadOnlyDictionary<string, DataTable> tables) =>
        GeneratedTables = tables;

    public void SetParameters(IReadOnlyDictionary<string, object?> parameters) =>
        Parameters = parameters;

    public void Clear()
    {
        FilePath = null;
        Metadata = null;
        GeneratedTables = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }
}
