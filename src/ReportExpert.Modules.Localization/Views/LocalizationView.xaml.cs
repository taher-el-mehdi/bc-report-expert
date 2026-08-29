using ReportExpert.Modules.Localization.ViewModels;

namespace ReportExpert.Modules.Localization.Views;

public partial class LocalizationView : System.Windows.Controls.UserControl
{
    public LocalizationViewModel ViewModel { get; }

    public LocalizationView()
    {
        InitializeComponent();
        ViewModel = new LocalizationViewModel();
        DataContext = ViewModel;
    }

    public void SetReportContext(string path) => ViewModel.SetReportContext(path);
}
