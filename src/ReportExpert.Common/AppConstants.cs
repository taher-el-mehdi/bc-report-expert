namespace ReportExpert.Common;

/// <summary>
/// Application-wide names used for AppData folders, settings files, and UI theme values.
/// Centralizing these avoids magic strings across modules.
/// </summary>
public static class AppConstants
{
    /// <summary>Product display name shown in the UI and window chrome.</summary>
    public const string ApplicationName = "Report Expert";

    /// <summary>Folder name under <c>%AppData%</c> for user settings and caches.</summary>
    public const string AppDataFolderName = "ReportExpert";

    /// <summary>Primary application settings file (theme, preview defaults).</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>Copilot provider configuration (API key stored locally — never commit).</summary>
    public const string CopilotSettingsFileName = "copilot.json";

    /// <summary>Recent RDLC file list.</summary>
    public const string RecentFilesFileName = "recent.json";

    /// <summary>Recent project folder list.</summary>
    public const string RecentProjectsFileName = "recent-projects.json";

    /// <summary>WebView2 user-data subdirectory under the AppData folder.</summary>
    public const string WebView2FolderName = "WebView2";

    /// <summary>Temp subdirectory used for generated preview PDFs.</summary>
    public const string TempPreviewFolderName = "Preview";

    /// <summary>Temp subdirectory used for extracted AL TextMate grammars.</summary>
    public const string TempGrammarsFolderName = "Grammars";

    /// <summary>
    /// Returns <c>%AppData%\ReportExpert</c>, creating the directory if needed.
    /// </summary>
    public static string GetAppDataDirectory()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Combines <see cref="GetAppDataDirectory"/> with a file or folder name.</summary>
    public static string GetAppDataPath(string relativeName) =>
        Path.Combine(GetAppDataDirectory(), relativeName);

    /// <summary>
    /// Returns <c>%TEMP%\ReportExpert\{segments...}</c>, creating the directory if needed.
    /// </summary>
    public static string GetTempDirectory(params string[] segments)
    {
        string[] parts = new string[segments.Length + 2];
        parts[0] = Path.GetTempPath();
        parts[1] = AppDataFolderName;
        Array.Copy(segments, 0, parts, 2, segments.Length);
        string path = Path.Combine(parts);
        Directory.CreateDirectory(path);
        return path;
    }
}

/// <summary>WPF-UI / Settings theme identifiers persisted in settings.json.</summary>
public static class ThemeNames
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";
}
