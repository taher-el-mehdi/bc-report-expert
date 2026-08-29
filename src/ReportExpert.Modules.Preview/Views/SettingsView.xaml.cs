using System.Windows;
using System.Windows.Controls;
using ReportExpert.Mcp.Client;
using ReportExpert.Modules.Copilot.Services;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.Modules.Preview.ViewModels;

namespace ReportExpert.Modules.Preview.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible || _viewModel is null)
                return;

            _viewModel.ReloadCopilotFromService();
            ApiKeyBox.Password = _viewModel.ApiKey;
        };
    }

    public void Initialize(
        SettingsService settingsService,
        CopilotSettingsService copilotSettingsService,
        Action? onSettingsSaved = null,
        Action? reloadCopilot = null,
        IMcpServerHost? mcpServers = null,
        Func<Task>? restartMcpAsync = null,
        Action? onToolEnablementChanged = null)
    {
        _viewModel = new SettingsViewModel(settingsService, copilotSettingsService);
        _viewModel.SettingsSaved += () =>
        {
            onSettingsSaved?.Invoke();
            reloadCopilot?.Invoke();
        };
        DataContext = _viewModel;
        ApiKeyBox.Password = _viewModel.ApiKey;
        _viewModel.AttachMcp(mcpServers, restartMcpAsync, onToolEnablementChanged);
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.ApiKey = ApiKeyBox.Password;
    }
}
