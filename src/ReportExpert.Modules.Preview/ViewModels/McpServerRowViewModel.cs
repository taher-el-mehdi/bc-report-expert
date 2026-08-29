using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ReportExpert.Mcp.Client;
using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Preview.ViewModels;

/// <summary>One MCP server row in Settings, expandable to its tools.</summary>
public partial class McpServerRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Creates a row from a host status snapshot.</summary>
    public McpServerRowViewModel(
        McpServerStatus status,
        CopilotSettingsService settingsService,
        Action? onToolEnablementChanged)
    {
        Id = status.Id;
        Command = status.Command;
        LastError = status.LastError ?? string.Empty;
        ToolCount = status.ToolCount;

        foreach (var tool in status.Tools)
        {
            Tools.Add(new McpToolRowViewModel(
                tool,
                settingsService.Settings.IsMcpToolEnabled(tool.QualifiedName),
                enabled =>
                {
                    if (!settingsService.Settings.SetMcpToolEnabled(tool.QualifiedName, enabled))
                        return;

                    settingsService.Save();
                    onToolEnablementChanged?.Invoke();
                }));
        }

        EnabledToolCount = Tools.Count(tool => tool.IsEnabled);
    }

    /// <summary>The server identifier.</summary>
    public string Id { get; }

    /// <summary>The executable path.</summary>
    public string Command { get; }

    /// <summary>How many tools the server reported.</summary>
    public int ToolCount { get; }

    /// <summary>How many of those tools are currently enabled.</summary>
    public int EnabledToolCount { get; private set; }

    /// <summary>A short caption like "31 tools" or "28 of 31 tools enabled".</summary>
    public string ToolCountLabel =>
        EnabledToolCount == ToolCount
            ? $"{ToolCount} tools"
            : $"{EnabledToolCount} of {ToolCount} tools enabled";

    /// <summary>The last connection error, or empty.</summary>
    public string LastError { get; }

    /// <summary>Whether an error line should show.</summary>
    public bool HasError => LastError.Length > 0;

    /// <summary>The tools under this server.</summary>
    public ObservableCollection<McpToolRowViewModel> Tools { get; } = [];

    /// <summary>Recalculates the enabled count after a toggle.</summary>
    public void RefreshEnabledCount()
    {
        EnabledToolCount = Tools.Count(tool => tool.IsEnabled);
        OnPropertyChanged(nameof(EnabledToolCount));
        OnPropertyChanged(nameof(ToolCountLabel));
    }
}

/// <summary>One tool under a server, with an enable switch.</summary>
public partial class McpToolRowViewModel : ObservableObject
{
    private readonly Action<bool> _onEnabledChanged;
    private bool _suppress;

    /// <summary>Creates a tool row.</summary>
    public McpToolRowViewModel(McpToolStatus status, bool isEnabled, Action<bool> onEnabledChanged)
    {
        Name = status.Name;
        QualifiedName = status.QualifiedName;
        Description = status.Description;
        _isEnabled = isEnabled;
        _onEnabledChanged = onEnabledChanged;
    }

    /// <summary>The bare tool name.</summary>
    public string Name { get; }

    /// <summary>The qualified name used for persistence.</summary>
    public string QualifiedName { get; }

    /// <summary>What the tool does.</summary>
    public string Description { get; }

    /// <summary>Whether a description line should show.</summary>
    public bool HasDescription => Description.Length > 0;

    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        if (_suppress)
            return;

        _onEnabledChanged(value);
    }

    /// <summary>Updates the switch without writing settings again.</summary>
    public void SetEnabledWithoutNotify(bool enabled)
    {
        _suppress = true;
        IsEnabled = enabled;
        _suppress = false;
    }
}
