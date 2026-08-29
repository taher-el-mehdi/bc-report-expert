using System.Windows;
using ReportExpert.Modules.Preview.Views.Dialogs;

namespace ReportExpert.Modules.Preview.Helpers;

public static class DialogService
{
    public static async Task ShowErrorAsync(
        Window? owner,
        string title,
        string description,
        string? solution = null,
        Exception? exception = null)
    {
        var dialog = new ErrorDialog
        {
            Owner = owner,
            Title = title,
            Problem = title,
            Description = description,
            Solution = solution ?? "Check the RDLC file and generated data, then try again.",
            TechnicalDetails = exception?.ToString() ?? string.Empty
        };

        await dialog.ShowAsync();
    }

    public static void ShowInfo(Window owner, string message, string title = "ReportPreview")
    {
        System.Windows.MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
