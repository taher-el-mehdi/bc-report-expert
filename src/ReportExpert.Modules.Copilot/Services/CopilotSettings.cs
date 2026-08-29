using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReportExpert.Common;
using ReportExpert.Modules.Copilot.Models;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>User-configurable AI provider settings persisted to disk.</summary>
public sealed class CopilotSettings
{
    public CopilotProvider Provider { get; set; } = CopilotProvider.Groq;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = CopilotProviderDefaults.DefaultModel(CopilotProvider.Groq);
    public string BaseUrl { get; set; } = CopilotProviderDefaults.DefaultBaseUrl(CopilotProvider.Groq);
    public string AzureDeployment { get; set; } = string.Empty;
    public bool AgentModeEnabled { get; set; }

    /// <summary>
    /// Qualified MCP tool names (<c>server__tool</c>) the user has turned off in Settings.
    /// Disabled tools are hidden from the model and cannot be executed.
    /// </summary>
    public List<string> DisabledMcpTools { get; set; } = [];

    /// <summary>Whether the given qualified tool name is allowed to run.</summary>
    public bool IsMcpToolEnabled(string qualifiedName)
    {
        if (string.IsNullOrEmpty(qualifiedName) || DisabledMcpTools.Count == 0)
            return true;

        return !DisabledMcpTools.Contains(qualifiedName, StringComparer.Ordinal);
    }

    /// <summary>Enables or disables a tool and returns whether the set changed.</summary>
    public bool SetMcpToolEnabled(string qualifiedName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return false;

        if (enabled)
            return DisabledMcpTools.RemoveAll(name =>
                string.Equals(name, qualifiedName, StringComparison.Ordinal)) > 0;

        if (DisabledMcpTools.Contains(qualifiedName, StringComparer.Ordinal))
            return false;

        DisabledMcpTools.Add(qualifiedName);
        return true;
    }
}

/// <summary>
/// Persists <see cref="CopilotSettings"/> to
/// <c>%AppData%\ReportExpert\copilot.json</c> (local only — never commit).
/// </summary>
public sealed class CopilotSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;

    public CopilotSettings Settings { get; private set; } = new();

    public CopilotSettingsService()
    {
        _settingsPath = AppConstants.GetAppDataPath(AppConstants.CopilotSettingsFileName);
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Settings = new CopilotSettings();
            return;
        }

        try
        {
            Settings = JsonSerializer.Deserialize<CopilotSettings>(File.ReadAllText(_settingsPath), JsonOptions)
                       ?? new CopilotSettings();
        }
        catch (Exception)
        {
            // Prefer defaults over crashing startup on a corrupt Copilot settings file.
            Settings = new CopilotSettings();
        }
    }

    public void Save() =>
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
}
