using System.IO;
using System.Windows;
using System.Windows.Threading;
using ReportExpert.App.Assets;
using ReportExpert.App.Shell;
using ReportExpert.App.Workspace;
using ReportExpert.Common;
using ReportExpert.Domain.Models;
using ReportExpert.Editors.Editors;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.Modules.Preview.Views.Dialogs;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ReportExpert.App;

public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _shellViewModel;
    private readonly WorkspaceService _workspaceService;
    private readonly SettingsService _settingsService;
    private readonly ShellCoordinator _coordinator;

    public MainWindow()
    {
        InitializeComponent();

        _workspaceService = new WorkspaceService();
        _settingsService = new SettingsService();
        EditorServiceBootstrap.Initialize();
        ThemeLogo.Bind(AppLogo);

        _shellViewModel = new ShellViewModel(_workspaceService, () => this);
        DataContext = _shellViewModel;
        HomeFeature.DataContext = _shellViewModel;
        WorkspaceExplorer.DataContext = _shellViewModel;

        _coordinator = new ShellCoordinator(
            HomeFeature.HomeCopilotPanel,
            _workspaceService,
            _shellViewModel.HomeViewer);
        _coordinator.Initialize();

        _shellViewModel.FileOpenRequested += OnWorkspaceFileOpenRequested;
        _shellViewModel.WorkspaceChanged += OnWorkspaceChanged;
        _shellViewModel.HomeViewer.OpenLayoutInPreviewRequested += OnOpenLayoutInPreviewRequested;

        SettingsFeature.Initialize(
            _settingsService,
            HomeFeature.HomeCopilotPanel.ViewModel.CopilotSettingsService,
            onSettingsSaved: () =>
            {
                ApplyThemeFromSettings();
                HomeFeature.HomeCopilotPanel.ViewModel.ReloadSettings();
                HomeFeature.ApplyDesignerViewSettings();
            },
            reloadCopilot: () => HomeFeature.HomeCopilotPanel.ViewModel.ReloadSettings(),
            mcpServers: _coordinator.McpServers,
            restartMcpAsync: _coordinator.RestartMcpAsync);

        HomeFeature.AttachSettings(_settingsService);

        // The MCP servers are child processes. Closing the window has to take them with it.
        Closed += async (_, _) => await _coordinator.DisposeAsync();
    }

    private void OnWorkspaceChanged(WorkspaceSnapshot? snapshot)
    {
        NavHome.IsChecked = true;
        if (snapshot is null)
            HomeFeature.HomeCopilotPanel.ViewModel.ClearReportContext();
    }

    private async void OnWorkspaceFileOpenRequested(WorkspaceFileEntry entry)
    {
        try
        {
            NavHome.IsChecked = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await _shellViewModel.HomeViewer.OpenAsync(entry);
            _coordinator.NotifyReportOpened(entry.FullPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open '{entry.Name}':\n\n{ex.Message}",
                AppConstants.ApplicationName,
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnOpenLayoutInPreviewRequested(string layoutPath)
    {
        try
        {
            _coordinator.NotifyReportOpened(layoutPath);
            RdlcPreviewWindow.ShowDialog(layoutPath, owner: this, settings: _settingsService);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open preview for '{Path.GetFileName(layoutPath)}':\n\n{ex.Message}",
                AppConstants.ApplicationName,
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyThemeFromSettings()
    {
        string theme = _settingsService.Settings.Theme;
        if (theme == ThemeNames.System)
            ApplicationThemeManager.ApplySystemTheme();
        else
        {
            ApplicationTheme appTheme = theme == ThemeNames.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(appTheme, WindowBackdropType.Mica);
        }

        ThemeLogo.Update(AppLogo);
        EditorServicesLocator.ThemeService?.NotifyThemeChanged();
    }
}
