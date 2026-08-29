using ReportExpert.Core.Shell;

namespace ReportExpert.Core.Shell;

public sealed class ShellBuilder : IShellBuilder
{
    public List<SidebarRegistration> Sidebars { get; } = [];
    public List<ToolPanelRegistration> ToolPanels { get; } = [];
    public List<IStatusBarProvider> StatusBarProviders { get; } = [];
    public List<ICommandDescriptor> Commands { get; } = [];

    public void RegisterSidebar(string id, Type viewModelType, string iconKey, int order, bool isEnabled = true) =>
        Sidebars.Add(new SidebarRegistration(id, viewModelType, iconKey, order, isEnabled));

    public void RegisterToolPanel(string id, Type viewModelType, DockTarget target, int order) =>
        ToolPanels.Add(new ToolPanelRegistration(id, viewModelType, target, order));

    public void RegisterStatusBarProvider(IStatusBarProvider provider) =>
        StatusBarProviders.Add(provider);

    public void RegisterCommand(ICommandDescriptor descriptor) =>
        Commands.Add(descriptor);
}

public sealed record SidebarRegistration(string Id, Type ViewModelType, string IconKey, int Order, bool IsEnabled);
public sealed record ToolPanelRegistration(string Id, Type ViewModelType, DockTarget Target, int Order);
