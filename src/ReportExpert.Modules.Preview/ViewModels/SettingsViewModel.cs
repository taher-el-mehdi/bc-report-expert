using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.Common;
using ReportExpert.Mcp.Client;
using ReportExpert.Modules.Copilot.Models;
using ReportExpert.Modules.Copilot.Services;
using ReportExpert.Modules.Preview.Services;
using Wpf.Ui.Controls;

namespace ReportExpert.Modules.Preview.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly HttpClient TestHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly SettingsService _settingsService;
    private readonly CopilotSettingsService _copilotSettingsService;
    private bool _suppressAutoSave = true;
    private CancellationTokenSource? _autoSaveCts;
    private IMcpServerHost? _mcpHost;
    private Func<Task>? _restartMcp;
    private Action? _onToolEnablementChanged;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private string _defaultExportFolder;

    [ObservableProperty]
    private int _rowsGenerated;

    [ObservableProperty]
    private bool _isCustomRowCount;

    [ObservableProperty]
    private int _fontSize;

    [ObservableProperty]
    private int _maxRecentFiles;

    [ObservableProperty]
    private bool _rememberRecentFiles;

    [ObservableProperty]
    private bool _autoPreview;

    [ObservableProperty]
    private bool _alSourcePanelVisible;

    [ObservableProperty]
    private bool _showDesignerRulers = true;

    [ObservableProperty]
    private bool _showDesignerGridlines = true;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private CopilotProvider _selectedProvider = CopilotProvider.Groq;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _apiKeyTestMessage = string.Empty;

    [ObservableProperty]
    private bool _isTestingApiKey;

    [ObservableProperty]
    private bool _agentModeEnabled;

    [ObservableProperty]
    private bool _isRefreshingMcp;

    [ObservableProperty]
    private string _mcpConfigurationPath = string.Empty;

    /// <summary>The MCP servers and whether each is running.</summary>
    public ObservableCollection<McpServerRowViewModel> McpServers { get; } = [];

    /// <summary>Models for the currently selected provider.</summary>
    public ObservableCollection<AiModelOption> ModelOptions { get; } = [];

    /// <summary>Providers shown in the Copilot combo box.</summary>
    public IReadOnlyList<CopilotProvider> ProviderOptions { get; } = CopilotProviderDefaults.AllProviders;

    public string[] ThemeOptions { get; } = [ThemeNames.System, ThemeNames.Light, ThemeNames.Dark];

    public ControlAppearance Rows10Appearance =>
        !IsCustomRowCount && RowsGenerated == 10 ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public ControlAppearance Rows50Appearance =>
        !IsCustomRowCount && RowsGenerated == 50 ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public ControlAppearance RowsCustomAppearance =>
        IsCustomRowCount ? ControlAppearance.Primary : ControlAppearance.Secondary;

    public string ProviderDisplayName => CopilotProviderDefaults.DisplayName(SelectedProvider);

    public string GetApiKeyToolTip => $"Open {ProviderDisplayName} to create an API key";

    public event Action? SettingsSaved;

    public SettingsViewModel(SettingsService settingsService, CopilotSettingsService copilotSettingsService)
    {
        _settingsService = settingsService;
        _copilotSettingsService = copilotSettingsService;

        var s = settingsService.Settings;
        _theme = s.Theme;
        _defaultExportFolder = s.DefaultExportFolder;
        _rowsGenerated = Math.Clamp(s.RowsGenerated, 1, 500);
        _isCustomRowCount = _rowsGenerated is not 10 and not 50;
        _fontSize = s.FontSize;
        _maxRecentFiles = s.MaxRecentFiles;
        _rememberRecentFiles = s.RememberRecentFiles;
        _autoPreview = s.AutoPreview;
        _alSourcePanelVisible = s.AlSourcePanelVisible;
        _showDesignerRulers = s.ShowDesignerRulers;
        _showDesignerGridlines = s.ShowDesignerGridlines;

        var c = copilotSettingsService.Settings;
        _apiKey = c.ApiKey;
        _selectedProvider = c.Provider;
        RefreshModelOptions(_selectedProvider);
        _model = NormalizeModelId(c.Model);
        _agentModeEnabled = c.AgentModeEnabled;

        _suppressAutoSave = false;
    }

    /// <summary>
    /// Connects the MCP section to the running servers.
    /// </summary>
    /// <param name="host">The server host, or <see langword="null"/> when the shell has none.</param>
    /// <param name="restart">Restarts the servers and reconnects the assistant.</param>
    /// <param name="onToolEnablementChanged">
    /// Called when the user toggles a tool so the assistant picks up the new filter immediately.
    /// </param>
    public void AttachMcp(
        IMcpServerHost? host,
        Func<Task>? restart = null,
        Action? onToolEnablementChanged = null)
    {
        _mcpHost = host;
        _restartMcp = restart;
        _onToolEnablementChanged = onToolEnablementChanged;
        McpConfigurationPath = host?.ConfigurationPath ?? string.Empty;

        _ = RefreshMcpAsync();
    }

    /// <summary>
    /// Pulls Copilot provider/model/key from the shared settings service (e.g. after Rex chat changes the model).
    /// </summary>
    public void ReloadCopilotFromService()
    {
        _suppressAutoSave = true;
        try
        {
            var c = _copilotSettingsService.Settings;
            ApiKey = c.ApiKey;
            SelectedProvider = c.Provider;
            RefreshModelOptions(c.Provider);
            Model = NormalizeModelId(c.Model);
            AgentModeEnabled = c.AgentModeEnabled;
            OnPropertyChanged(nameof(ProviderDisplayName));
            OnPropertyChanged(nameof(GetApiKeyToolTip));
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    /// <summary>Re-reads the state of each MCP server.</summary>
    [RelayCommand]
    private async Task RefreshMcpAsync()
    {
        if (_mcpHost is null)
            return;

        IsRefreshingMcp = true;

        try
        {
            var statuses = await _mcpHost.DescribeAsync();
            var expandedIds = McpServers
                .Where(server => server.IsExpanded)
                .Select(server => server.Id)
                .ToHashSet(StringComparer.Ordinal);

            McpServers.Clear();
            foreach (var status in statuses)
            {
                var row = new McpServerRowViewModel(
                    status,
                    _copilotSettingsService,
                    OnToolEnablementChanged);

                if (expandedIds.Contains(row.Id))
                    row.IsExpanded = true;

                McpServers.Add(row);
            }
        }
        catch (Exception)
        {
            // The list is diagnostic. Failing to read it must not take the settings screen down.
            McpServers.Clear();
        }
        finally
        {
            IsRefreshingMcp = false;
        }
    }

    /// <summary>Restarts the MCP servers, picking up any hand edits to the configuration.</summary>
    [RelayCommand]
    private async Task RestartMcpAsync()
    {
        if (_restartMcp is null)
            return;

        IsRefreshingMcp = true;

        try
        {
            await _restartMcp();
        }
        finally
        {
            IsRefreshingMcp = false;
        }

        await RefreshMcpAsync();
    }

    /// <summary>Opens the server list in the user's editor.</summary>
    [RelayCommand]
    private void EditMcpConfiguration()
    {
        if (McpConfigurationPath.Length == 0)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(McpConfigurationPath) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No association for .json, or the file was removed. Nothing useful to do about it.
        }
    }

    private void OnToolEnablementChanged()
    {
        foreach (var server in McpServers)
            server.RefreshEnabledCount();

        _onToolEnablementChanged?.Invoke();
    }

    [RelayCommand]
    private void GetApiKey()
    {
        try
        {
            Process.Start(new ProcessStartInfo(CopilotProviderDefaults.ApiKeyUrl(SelectedProvider))
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            StatusMessage = "Could not open the API key page.";
        }
    }

    partial void OnSelectedProviderChanged(CopilotProvider value)
    {
        OnPropertyChanged(nameof(ProviderDisplayName));
        OnPropertyChanged(nameof(GetApiKeyToolTip));
        RefreshModelOptions(value);
        Model = NormalizeModelId(Model);
    }

    private void RefreshModelOptions(CopilotProvider provider)
    {
        ModelOptions.Clear();
        foreach (var option in CopilotProviderDefaults.Models(provider))
            ModelOptions.Add(option);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_suppressAutoSave)
            return;

        if (e.PropertyName is nameof(StatusMessage) or nameof(ApiKeyTestMessage) or nameof(IsTestingApiKey)
            or nameof(Rows10Appearance) or nameof(Rows50Appearance) or nameof(RowsCustomAppearance)
            or nameof(IsCustomRowCount) or nameof(IsRefreshingMcp) or nameof(McpConfigurationPath)
            or nameof(ProviderDisplayName) or nameof(GetApiKeyToolTip) or null)
            return;

        if (e.PropertyName == nameof(RowsGenerated))
        {
            int clamped = Math.Clamp(RowsGenerated, 1, 500);
            if (clamped != RowsGenerated)
            {
                RowsGenerated = clamped;
                return;
            }

            if (!IsCustomRowCount)
                IsCustomRowCount = clamped is not 10 and not 50;

            NotifyRowAppearances();
        }

        ScheduleAutoSave();
    }

    partial void OnIsCustomRowCountChanged(bool value) => NotifyRowAppearances();

    [RelayCommand]
    private void SetRowCount(string? preset)
    {
        if (!int.TryParse(preset, out int rows))
            return;

        IsCustomRowCount = false;
        if (RowsGenerated == rows)
        {
            NotifyRowAppearances();
            return;
        }

        RowsGenerated = rows;
    }

    [RelayCommand]
    private void UseCustomRowCount()
    {
        IsCustomRowCount = true;
        NotifyRowAppearances();
    }

    private void NotifyRowAppearances()
    {
        OnPropertyChanged(nameof(Rows10Appearance));
        OnPropertyChanged(nameof(Rows50Appearance));
        OnPropertyChanged(nameof(RowsCustomAppearance));
    }

    [RelayCommand]
    private void BrowseExportFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default export folder",
            SelectedPath = DefaultExportFolder
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DefaultExportFolder = dlg.SelectedPath;
    }

    private bool CanTestApiKey() => !IsTestingApiKey;

    partial void OnIsTestingApiKeyChanged(bool value) =>
        TestApiKeyCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanTestApiKey))]
    private async Task TestApiKeyAsync()
    {
        string key = ApiKey.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ApiKeyTestMessage = "Enter an API key first.";
            return;
        }

        string model = string.IsNullOrWhiteSpace(Model)
            ? CopilotProviderDefaults.DefaultModel(SelectedProvider)
            : Model.Trim();

        IsTestingApiKey = true;
        ApiKeyTestMessage = "Testing…";

        try
        {
            string url = $"{CopilotProviderDefaults.DefaultBaseUrl(SelectedProvider).TrimEnd('/')}/chat/completions";
            var payload = new
            {
                model,
                messages = new[] { new { role = "user", content = "Reply with OK only." } },
                max_tokens = 5,
                temperature = 0
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            if (SelectedProvider == CopilotProvider.AzureOpenAI)
                request.Headers.Add("api-key", key);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await TestHttp.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                ApiKeyTestMessage = "API key is valid.";
                StatusMessage = "API key OK";
            }
            else
            {
                string detail = TryExtractError(body) ?? body;
                if (detail.Length > 180)
                    detail = detail[..180] + "…";
                ApiKeyTestMessage = $"Failed ({(int)response.StatusCode}): {detail}";
                StatusMessage = "API key test failed";
            }
        }
        catch (Exception ex)
        {
            ApiKeyTestMessage = $"Failed: {ex.Message}";
            StatusMessage = "API key test failed";
        }
        finally
        {
            IsTestingApiKey = false;
        }
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _ = PersistAfterDelayAsync(token);
    }

    private async Task PersistAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            Persist();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer change
        }
    }

    private void Persist()
    {
        _settingsService.Settings.Theme = Theme;
        _settingsService.Settings.DefaultExportFolder = DefaultExportFolder;
        _settingsService.Settings.RowsGenerated = Math.Clamp(RowsGenerated, 1, 500);
        _settingsService.Settings.FontSize = FontSize;
        _settingsService.Settings.MaxRecentFiles = Math.Clamp(MaxRecentFiles, 1, 50);
        _settingsService.Settings.RememberRecentFiles = RememberRecentFiles;
        _settingsService.Settings.AutoPreview = AutoPreview;
        _settingsService.Settings.AlSourcePanelVisible = AlSourcePanelVisible;
        _settingsService.Settings.ShowDesignerRulers = ShowDesignerRulers;
        _settingsService.Settings.ShowDesignerGridlines = ShowDesignerGridlines;
        _settingsService.Save();

        _copilotSettingsService.Settings.Provider = SelectedProvider;
        _copilotSettingsService.Settings.ApiKey = ApiKey.Trim();
        _copilotSettingsService.Settings.Model = NormalizeModelId(Model);
        _copilotSettingsService.Settings.BaseUrl = CopilotProviderDefaults.DefaultBaseUrl(SelectedProvider);
        _copilotSettingsService.Settings.AzureDeployment = string.Empty;
        _copilotSettingsService.Settings.AgentModeEnabled = AgentModeEnabled;
        _copilotSettingsService.Save();

        StatusMessage = "Saved";
        SettingsSaved?.Invoke();
    }

    private string NormalizeModelId(string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId) &&
            ModelOptions.Any(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
            return modelId!;

        return CopilotProviderDefaults.DefaultModel(SelectedProvider);
    }

    private static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.TryGetProperty("message", out var message) ? message.GetString() : error.ToString();
        }
        catch (JsonException)
        {
            // Provider error bodies are not always JSON; fall back to the raw snippet.
        }

        return null;
    }
}
