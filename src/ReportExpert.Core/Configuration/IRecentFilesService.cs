using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Configuration;

public interface IRecentFilesService
{
    IReadOnlyList<RecentFileEntry> Entries { get; }
    void Load();
    void Add(string path);
    void Save();
}
