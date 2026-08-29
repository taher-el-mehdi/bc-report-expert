using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using ReportExpert.Common;
using ReportExpert.Modules.Preview.Services;
using ReportExpert.Modules.Preview.ViewModels;
using Wpf.Ui.Controls;

namespace ReportExpert.Modules.Preview.Views.Dialogs;

public partial class RdlcPreviewWindow : FluentWindow
{
    private readonly RdlcPreviewDialogViewModel _viewModel;
    private readonly string _rdlcPath;
    private bool _webViewReady;
    private bool _initialized;

    public RdlcPreviewWindow(string rdlcPath, SettingsService? settings = null, Window? owner = null)
    {
        InitializeComponent();

        _rdlcPath = rdlcPath;
        if (owner is not null)
            Owner = owner;

        _viewModel = new RdlcPreviewDialogViewModel(settings ?? new SettingsService());
        DataContext = _viewModel;
        _viewModel.PdfReady += OnPdfReady;

        Loaded += OnLoadedAsync;
        Closed += OnClosed;
    }

    /// <summary>Shows a modal RDLC PDF preview window (sample data, auto-generated).</summary>
    public static void ShowDialog(string rdlcPath, Window? owner = null, SettingsService? settings = null)
    {
        var window = new RdlcPreviewWindow(rdlcPath, settings, owner);
        window.ShowDialog();
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;
        await InitWebViewAsync();
        await _viewModel.InitializeAsync(_rdlcPath);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PdfReady -= OnPdfReady;
        try
        {
            PdfViewer.Dispose();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            string userDataFolder = AppConstants.GetAppDataPath(AppConstants.WebView2FolderName);
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await PdfViewer.EnsureCoreWebView2Async(env);
            _webViewReady = true;
        }
        catch (Exception)
        {
            _webViewReady = false;
            _viewModel.StatusMessage =
                "PDF will open in your default viewer. Install the WebView2 Runtime for in-window preview.";
        }
    }

    private void OnPdfReady(string pdfPath)
    {
        Dispatcher.Invoke(() => ShowPdfPreview(pdfPath));
    }

    private void ShowPdfPreview(string pdfPath)
    {
        if (!_webViewReady || PdfViewer.CoreWebView2 is null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // File was still written; Download remains available.
            }

            return;
        }

        PdfViewer.Visibility = Visibility.Visible;
        PdfViewer.CoreWebView2.Navigate(new Uri(pdfPath).AbsoluteUri);
    }
}
