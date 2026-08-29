using System.IO;
using System.Windows;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Views;

namespace ReportExpert.LayoutSmoke;

/// <summary>
/// Hosts the real PreviewView and loads each test-reports layout, catching crashes
/// that only appear with WindowsFormsHost / WPF bindings.
/// </summary>
internal static class UiSmoke
{
    [STAThread]
    public static int Run(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Folder not found: {folder}");
            return 1;
        }

        var app = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        int failures = 0;
        var window = new Window
        {
            Title = "ReportExpert Preview UI Smoke",
            Width = 1100,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var preview = new PreviewView();
        window.Content = preview;
        window.Show();
        window.Activate();

        // Let WPF finish initial layout / WinForms handle creation.
        DoEvents(app);

        foreach (string path in Directory.EnumerateFiles(folder)
                     .Where(LayoutFileHelper.IsSupportedLayout)
                     .OrderBy(LayoutFileHelper.MatchRank)
                     .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(path);
            Console.WriteLine($"UI load: {name}");
            Console.Out.Flush();

            try
            {
                Task loadTask = preview.OpenReportAsync(path);
                // Pump the dispatcher while waiting — GetResult() alone deadlocks the STA UI thread.
                var started = DateTime.UtcNow;
                while (!loadTask.IsCompleted)
                {
                    if ((DateTime.UtcNow - started).TotalSeconds > 60)
                        throw new TimeoutException($"Timed out loading {name}");
                    DoEvents(app);
                    Thread.Sleep(15);
                }

                loadTask.GetAwaiter().GetResult();
                DoEvents(app);

                Console.WriteLine(
                    $"  OK kind={preview.ViewModel.CurrentLayoutKind} hasReport={preview.ViewModel.HasReport} status={preview.ViewModel.StatusMessage}");

                if (!preview.ViewModel.HasReport)
                    throw new InvalidOperationException("HasReport stays false after load.");
                if (preview.ViewModel.IsBusy)
                    throw new InvalidOperationException("IsBusy still true after load completed.");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException is not null)
                    Console.WriteLine($"       Inner: {ex.InnerException.Message}");
            }
        }

        preview.DisposeViewer();
        window.Close();
        app.Shutdown();

        Console.WriteLine(failures == 0 ? "UI SMOKE PASSED" : $"UI SMOKE FAILURES: {failures}");
        return failures == 0 ? 0 : 3;
    }

    private static void DoEvents(System.Windows.Application app)
    {
        app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
}
