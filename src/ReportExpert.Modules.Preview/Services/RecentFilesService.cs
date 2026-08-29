using System.IO;
using System.Text.Json;
using ReportExpert.Common;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Tracks recently opened RDLC paths under
/// <c>%AppData%\ReportExpert\recent.json</c>.
/// </summary>
public sealed class RecentFilesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _recentPath;
    private readonly SettingsService _settingsService;
    private List<RecentFileEntry> _entries = [];

    public IReadOnlyList<RecentFileEntry> Entries => _entries;

    public RecentFilesService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _recentPath = AppConstants.GetAppDataPath(AppConstants.RecentFilesFileName);
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_recentPath))
        {
            _entries = [];
            return;
        }

        try
        {
            string json = File.ReadAllText(_recentPath);
            _entries = JsonSerializer.Deserialize<List<RecentFileEntry>>(json) ?? [];
            _entries = _entries.Where(e => File.Exists(e.Path)).ToList();
        }
        catch (Exception)
        {
            // Corrupt recent-file cache — start empty rather than blocking open flows.
            _entries = [];
        }
    }

    public void Add(string path)
    {
        if (!_settingsService.Settings.RememberRecentFiles)
            return;

        _entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, new RecentFileEntry
        {
            Path = path,
            LastOpenedUtc = DateTime.UtcNow
        });

        int max = Math.Max(1, _settingsService.Settings.MaxRecentFiles);
        if (_entries.Count > max)
            _entries = _entries.Take(max).ToList();

        Save();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(_recentPath, json);
    }
}
