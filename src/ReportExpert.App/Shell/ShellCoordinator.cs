using System.IO;
using ReportExpert.App.Home;
using ReportExpert.App.Workspace;
using ReportExpert.Domain.Models;
using ReportExpert.Editors.Editors;
using ReportExpert.Editors.Services;
using ReportExpert.Mcp.Client;
using ReportExpert.Modules.Copilot.Views;
using ReportExpert.Modules.Preview.Helpers;

namespace ReportExpert.App.Shell;

/// <summary>
/// Wires cross-module events so Copilot shares the active RDLC path and workspace root, and owns
/// the MCP servers the assistant calls through.
/// </summary>
public sealed class ShellCoordinator : IAsyncDisposable
{
    private readonly CopilotView _copilot;
    private readonly WorkspaceService _workspaceService;
    private readonly HomeWorkspaceViewModel _homeViewer;
    private readonly McpHost _mcp = new();
    private string _currentReportPath = string.Empty;

    public ShellCoordinator(
        CopilotView copilot,
        WorkspaceService workspaceService,
        HomeWorkspaceViewModel homeViewer)
    {
        _copilot = copilot;
        _workspaceService = workspaceService;
        _homeViewer = homeViewer;
    }

    /// <summary>The MCP servers, for the settings screen to show and restart.</summary>
    public IMcpServerHost McpServers => _mcp;

    /// <summary>Subscribes module events. Call once at application startup.</summary>
    public void Initialize()
    {
        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;

        _copilot.ViewModel.GetCurrentXmlAsync = async () =>
        {
            if (_currentReportPath.Length > 0 &&
                File.Exists(_currentReportPath) &&
                LayoutFileHelper.IsRdlLayout(_currentReportPath))
                return await File.ReadAllTextAsync(_currentReportPath);

            return string.Empty;
        };

        _copilot.ViewModel.ReloadReportAsync = ReloadAfterAgentEditAsync;
        _copilot.ViewModel.AgentEditingChanged = editing => _homeViewer.IsAgentEditing = editing;

        _mcp.Start();
        _copilot.ViewModel.SetToolGateway(_mcp.Gateway);

        if (_workspaceService.Current is not null)
            _copilot.ViewModel.SetWorkspaceRoot(_workspaceService.Current.RootPath);
    }

    /// <summary>Re-reads the server list and reconnects. Called after the settings change.</summary>
    /// <returns>A task that completes once the servers are back.</returns>
    public async Task RestartMcpAsync()
    {
        await _mcp.RestartAsync();
        _copilot.ViewModel.SetToolGateway(_mcp.Gateway);
    }

    public void NotifyReportOpened(string path)
    {
        _currentReportPath = path ?? string.Empty;

        if (LayoutFileHelper.IsRdlLayout(_currentReportPath))
            _copilot.ViewModel.SetReportContext(_currentReportPath);
        else
            _copilot.ViewModel.ClearReportContext();
    }

    /// <summary>Shuts the MCP servers down.</summary>
    /// <returns>A task that completes once the child processes have exited.</returns>
    public ValueTask DisposeAsync() => _mcp.DisposeAsync();

    /// <summary>
    /// Reloads a report the assistant has just rewritten.
    /// </summary>
    /// <remarks>
    /// The editor holds the report in memory. Without this, the next save would write that stale
    /// copy straight over the assistant's changes.
    /// </remarks>
    private async Task ReloadAfterAgentEditAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (!string.Equals(_homeViewer.FilePath, path, StringComparison.OrdinalIgnoreCase))
            return;

        await _homeViewer.OpenLayoutAsync(path);
    }

    private void OnWorkspaceChanged(WorkspaceSnapshot? snapshot)
    {
        if (EditorServicesLocator.PathResolver is AlSourcePathResolver resolver)
            resolver.WorkspaceRoot = snapshot?.RootPath;

        _copilot.ViewModel.SetWorkspaceRoot(snapshot?.RootPath);

        if (snapshot is null)
        {
            _currentReportPath = string.Empty;
            _copilot.ViewModel.ClearReportContext();
        }
    }
}
