using System.Windows;
using System.Windows.Threading;
using ReportExpert.Common;
using ReportExpert.Modules.Preview.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ReportExpert.App;

/// <summary>WPF application entry — global exception handlers and startup theme.</summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ReportViewer PDF rendering needs Windows code pages (e.g. 1252).
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        base.OnStartup(e);
        ApplySavedTheme();
    }

    private static void ApplySavedTheme()
    {
        var settings = new SettingsService().Settings;
        switch (settings.Theme)
        {
            case ThemeNames.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
                break;
            case ThemeNames.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowCrashDialog("UI error", e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowCrashDialog("Fatal error", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowCrashDialog("Background error", e.Exception);
        e.SetObserved();
    }

    private static void ShowCrashDialog(string title, Exception ex)
    {
        try
        {
            System.Windows.MessageBox.Show(
                $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                $"{AppConstants.ApplicationName} — {title}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (Exception)
        {
            // Secondary failure while showing the crash dialog — nothing else we can do.
        }
    }
}
