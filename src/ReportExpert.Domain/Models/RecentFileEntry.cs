namespace ReportExpert.Domain.Models;

public sealed class RecentFileEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastOpenedUtc { get; set; }
}
