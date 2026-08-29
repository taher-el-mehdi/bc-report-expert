namespace ReportExpert.Domain.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public string DefaultExportFolder { get; set; } = string.Empty;
    public int RowsGenerated { get; set; } = 20;
    public int FontSize { get; set; } = 14;
    public bool RememberRecentFiles { get; set; } = true;
    public int MaxRecentFiles { get; set; } = 10;
    public bool AutoPreview { get; set; } = true;
}
