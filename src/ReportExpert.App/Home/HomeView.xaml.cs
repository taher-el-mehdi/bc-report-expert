using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ReportExpert.App.Assets;
using ReportExpert.App.Helpers;
using ReportExpert.App.Shell;
using ReportExpert.Domain.Models;
using ReportExpert.Modules.Copilot.Views;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Hosting;

namespace ReportExpert.App.Home;

public partial class HomeView : System.Windows.Controls.UserControl
{
    private readonly HomeOfficeDocumentController _office;
    private readonly IRdlcLayoutDesignerHost _rdlcDesigner = new RdlcLayoutDesignerHost();
    private HomeWorkspaceViewModel? _viewer;
    private FrameworkElement? _rdlcPage;
    private SettingsService? _settings;

    /// <summary>Home-embedded Copilot chat (shared with shell wiring).</summary>
    public CopilotView HomeCopilotPanel => HomeCopilot;

    public HomeView()
    {
        Resources.Add("RecentVisConverter", new CollectionCountToVisibilityConverter());
        InitializeComponent();
        _office = new HomeOfficeDocumentController(ExcelGrid, ExcelFallbackGrid, WordPreview);
        _rdlcDesigner.DirtyChanged += OnRdlcDirtyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Connects app settings so designer rulers/gridlines stay in sync.</summary>
    public void AttachSettings(SettingsService settings)
    {
        _settings = settings;
        ApplyDesignerViewSettings();
    }

    /// <summary>Re-reads designer chrome preferences after settings save.</summary>
    public void ApplyDesignerViewSettings()
    {
        if (_settings is null)
            return;

        var s = _settings.Settings;
        _rdlcDesigner.ApplyViewSettings(s.ShowDesignerRulers, s.ShowDesignerGridlines);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeLogo.Bind(HomeLogo);
        WireViewer();
        RequestDeferredEditorLoad();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => UnwireViewer();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => WireViewer();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            RequestDeferredEditorLoad();
    }

    private void WireViewer()
    {
        UnwireViewer();
        if (DataContext is ShellViewModel shell)
        {
            _viewer = shell.HomeViewer;
            _viewer.LayoutEditorLoadRequested += OnLayoutEditorLoadRequested;
            _viewer.LayoutEditorSaveRequested += OnLayoutEditorSaveRequested;
            _viewer.PropertyChanged += OnViewerPropertyChanged;
            _office.SetDirtyHandler(() => _viewer?.MarkDirty());
        }
    }

    private void UnwireViewer()
    {
        if (_viewer is null)
            return;

        _viewer.LayoutEditorLoadRequested -= OnLayoutEditorLoadRequested;
        _viewer.LayoutEditorSaveRequested -= OnLayoutEditorSaveRequested;
        _viewer.PropertyChanged -= OnViewerPropertyChanged;
        _office.SetDirtyHandler(null);
        _viewer = null;
    }

    private void OnViewerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeWorkspaceViewModel.IsExcelLayout)
            or nameof(HomeWorkspaceViewModel.IsWordLayout)
            or nameof(HomeWorkspaceViewModel.IsRdlcLayout)
            or nameof(HomeWorkspaceViewModel.FilePath))
        {
            RequestDeferredEditorLoad();
        }
    }

    private void OnLayoutEditorLoadRequested() => RequestDeferredEditorLoad();

    private void OnRdlcDirtyChanged(object? sender, EventArgs e)
    {
        if (_rdlcDesigner.IsDirty)
            _viewer?.MarkDirty();
    }

    private void RequestDeferredEditorLoad()
    {
        if (_viewer is null || !IsVisible)
            return;

        if (_viewer.IsExcelLayout || _viewer.IsWordLayout)
            Dispatcher.BeginInvoke(LoadOfficeDocumentNow, DispatcherPriority.Loaded);
        else if (_viewer.IsRdlcLayout)
            Dispatcher.BeginInvoke(LoadRdlcVisualNow, DispatcherPriority.Loaded);
        else
            ClearRdlcVisual();
    }

    private void LoadOfficeDocumentNow()
    {
        var viewer = _viewer;
        if (viewer is null)
            return;

        if (string.IsNullOrWhiteSpace(viewer.FilePath) || !File.Exists(viewer.FilePath))
            return;

        ClearRdlcVisual();

        try
        {
            _office.RunWithDirtySuppressed(() =>
            {
                if (viewer.IsExcelLayout)
                    _office.LoadExcel(viewer.FilePath, viewer.MarkLoaded);
                else if (viewer.IsWordLayout)
                    _office.LoadWord(viewer.FilePath, viewer.MarkLoaded);
            });
        }
        catch (Exception ex)
        {
            viewer.ReportEditorError($"Failed to open layout editor: {ex.Message}");
        }
    }

    private void LoadRdlcVisualNow()
    {
        var viewer = _viewer;
        if (viewer is null || !viewer.IsRdlcLayout)
            return;

        if (string.IsNullOrWhiteSpace(viewer.FilePath) || !File.Exists(viewer.FilePath))
            return;

        try
        {
            _rdlcDesigner.Load(viewer.FilePath);
            _rdlcPage = _rdlcDesigner.View;

            // Designer view fills the pane; internal canvas scrolls with the page.
            RdlcVisualHost.ClearValue(WidthProperty);
            RdlcVisualHost.ClearValue(HeightProperty);
            RdlcVisualHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            RdlcVisualHost.VerticalAlignment = VerticalAlignment.Stretch;
            RdlcVisualHost.Content = _rdlcPage;
            _rdlcPage.LayoutTransform = Transform.Identity;
            ApplyDesignerViewSettings();
            viewer.ClearStatus();
            viewer.MarkLoaded($"{Path.GetFileName(viewer.FilePath)} · Minimal layout editor");
        }
        catch (Exception ex)
        {
            _rdlcPage = null;
            RdlcVisualHost.Content = new Border
            {
                Padding = new Thickness(24),
                Child = new TextBlock
                {
                    Text = $"Could not build visual layout:\n{ex.Message}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = System.Windows.Media.Brushes.DarkRed
                }
            };
            viewer.ReportEditorError($"Failed to render RDLC layout: {ex.Message}");
        }
    }

    private void RdlcLayoutScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Designer view fills the host; page scrolling is handled inside the module.
    }

    private void ClearRdlcVisual()
    {
        _rdlcDesigner.Clear();
        _rdlcPage = null;
        if (RdlcVisualHost is not null)
        {
            RdlcVisualHost.Content = null;
            RdlcVisualHost.ClearValue(WidthProperty);
            RdlcVisualHost.ClearValue(HeightProperty);
        }
    }

    private void OnLayoutEditorSaveRequested()
    {
        if (_viewer is null || string.IsNullOrWhiteSpace(_viewer.FilePath))
            return;

        try
        {
            if (_viewer.IsRdlcLayout)
            {
                _rdlcDesigner.Save(_viewer.FilePath);
                _viewer.MarkSaved();
                return;
            }

            if (_viewer.IsExcelLayout)
            {
                if (!_office.TrySaveExcel(_viewer.FilePath, out string? error))
                {
                    _viewer.ReportEditorError(error!);
                    return;
                }

                _viewer.MarkSaved();
                return;
            }

            if (_viewer.IsWordLayout)
                _viewer.ReportEditorError(HomeOfficeDocumentController.WordReadOnlyMessage);
        }
        catch (Exception ex)
        {
            _viewer.ReportEditorError($"Save failed: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Could not save '{Path.GetFileName(_viewer.FilePath)}':\n\n{ex.Message}",
                "Report Expert",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenWordExternally_Click(object sender, RoutedEventArgs e)
    {
        if (_viewer is null || string.IsNullOrWhiteSpace(_viewer.FilePath) || !File.Exists(_viewer.FilePath))
            return;

        if (!HomeOfficeDocumentController.TryOpenExternally(_viewer.FilePath, out string? error))
        {
            System.Windows.MessageBox.Show(
                $"Could not open Word:\n\n{error}",
                "Report Expert",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RecentProject_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RecentProjectEntry entry } &&
            DataContext is ShellViewModel vm)
        {
            vm.OpenRecentProjectCommand.Execute(entry);
        }
    }
}
