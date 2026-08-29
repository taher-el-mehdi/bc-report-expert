using System.Collections.Specialized;
using System.Windows.Input;
using ReportExpert.Modules.Copilot.ViewModels;

namespace ReportExpert.Modules.Copilot.Views;

public partial class CopilotView : System.Windows.Controls.UserControl
{
    public CopilotViewModel ViewModel { get; }

    public CopilotView()
    {
        InitializeComponent();
        ViewModel = new CopilotViewModel();
        DataContext = ViewModel;

        ((INotifyCollectionChanged)ViewModel.Messages).CollectionChanged += (_, _) =>
            ChatScroll.ScrollToEnd();
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        // Shift+Enter inserts a new line; Enter alone sends.
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        e.Handled = true;
        if (ViewModel.SendCommand.CanExecute(null))
            ViewModel.SendCommand.Execute(null);
    }
}
