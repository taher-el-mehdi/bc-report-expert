using System.IO;
using System.Windows;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;
using ReportExpert.Modules.Preview.Rendering;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.Modules.Preview.ViewModels;
using Wpf.Ui;

namespace ReportExpert.Modules.Preview.Views;

public partial class PreviewView : System.Windows.Controls.UserControl
{
    private readonly SettingsService _settingsService;
    private readonly PreviewViewModel _viewModel;
    private readonly ReportViewerHost _reportViewerHost;

    public SettingsService SettingsService => _settingsService;

    /// <summary>Raised when a report is loaded, so the host shell can share it with other modules.</summary>
    public event Action<string>? ReportLoaded;

    public PreviewViewModel ViewModel => _viewModel;

    public PreviewView()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        var previewService = new ReportPreviewService();
        _reportViewerHost = new ReportViewerHost(previewService);
        PreviewHost.Content = _reportViewerHost;

        var snackbarService = new SnackbarService();
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        _viewModel = new PreviewViewModel(
            () => Window.GetWindow(this),
            previewService,
            _settingsService,
            snackbarService);
        DataContext = _viewModel;

        _viewModel.ReportOpened += path => ReportLoaded?.Invoke(path);

        Unloaded += (_, _) => { };
    }

    public Task OpenReportAsync(string layoutPath) => _viewModel.LoadReportAsync(layoutPath);

    public void DisposeViewer() => _reportViewerHost.Dispose();

    private void View_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void View_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        string? layout = files
            .Where(LayoutFileHelper.IsSupportedLayout)
            .OrderBy(LayoutFileHelper.MatchRank)
            .FirstOrDefault();

        if (layout is not null)
            await _viewModel.LoadReportFromDropAsync(layout);

        e.Handled = true;
    }

    private void ExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ExplorerTreeItem item)
            _viewModel.SelectedExplorerItem = item;
    }
}
