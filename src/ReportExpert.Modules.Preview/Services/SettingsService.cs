using System.IO;
using System.Text.Json;
using ReportExpert.Common;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> from
/// <c>%AppData%\ReportExpert\settings.json</c>.
/// Corrupt files reset to defaults so the app can still start.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        _settingsPath = AppConstants.GetAppDataPath(AppConstants.SettingsFileName);
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            _settings = new AppSettings();
            return;
        }

        try
        {
            string json = File.ReadAllText(_settingsPath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Prefer defaults over crashing startup on a corrupt settings file.
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
