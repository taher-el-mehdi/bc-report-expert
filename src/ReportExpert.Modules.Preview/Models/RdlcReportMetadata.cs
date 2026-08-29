namespace ReportExpert.Modules.Preview.Models;

public sealed class RdlcReportMetadata
{
    public string FilePath { get; init; } = string.Empty;
    public string ReportName { get; init; } = string.Empty;
    public string ReportVersion { get; init; } = string.Empty;
    public IReadOnlyList<RdlcDataSetInfo> DataSets { get; init; } = [];
    public IReadOnlyList<RdlcParameterInfo> Parameters { get; init; } = [];
    public double PageWidth { get; init; }
    public double PageHeight { get; init; }
    public string Orientation { get; init; } = "Portrait";
    public int EmbeddedImageCount { get; init; }
    public int ExpressionCount { get; init; }

    public string PageSizeDisplay =>
        PageWidth > 0 && PageHeight > 0
            ? $"{PageWidth:0.##} x {PageHeight:0.##} in ({Orientation})"
            : "Unknown";
}
