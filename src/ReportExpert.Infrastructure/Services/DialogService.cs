using System.Windows;
using Microsoft.Win32;
using ReportExpert.Core.Services;
using ReportExpert.Infrastructure.Views.Dialogs;

namespace ReportExpert.Infrastructure.Services;

public sealed class DialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string description, string? solution = null, Exception? exception = null)
    {
        var dialog = new ErrorDialog
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = title,
            Problem = title,
            Description = description,
            Solution = solution ?? "Check the RDLC file and generated data, then try again.",
            TechnicalDetails = exception?.ToString() ?? string.Empty
        };

        return dialog.ShowAsync();
    }

    public void ShowInfo(string message, string title = "Report Expert") =>
        System.Windows.MessageBox.Show(
            System.Windows.Application.Current.MainWindow,
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

    public Task<string?> ShowOpenFileDialogAsync(string filter, string title)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title };
        return Task.FromResult(dlg.ShowDialog() == true ? dlg.FileName : null);
    }

    public Task<string?> ShowSaveFileDialogAsync(string filter, string defaultFileName, string? initialDirectory)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            InitialDirectory = initialDirectory ?? string.Empty
        };
        return Task.FromResult(dlg.ShowDialog() == true ? dlg.FileName : null);
    }

    public Task<string?> ShowFolderBrowserDialogAsync(string description, string? selectedPath)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            SelectedPath = selectedPath ?? string.Empty
        };

        return Task.FromResult(dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null);
    }
}
