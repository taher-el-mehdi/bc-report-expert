using System.Windows;
using Wpf.Ui.Controls;

namespace ReportExpert.Modules.Preview.Views.Dialogs;

public partial class ErrorDialog : FluentWindow
{
    public string Problem { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;

    public ErrorDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ProblemText.Text = Problem;
            DescriptionText.Text = Description;
            SolutionText.Text = Solution;
            TechnicalText.Text = TechnicalDetails;
        };
    }

    public Task ShowAsync()
    {
        ShowDialog();
        return Task.CompletedTask;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
