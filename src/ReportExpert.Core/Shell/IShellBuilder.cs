namespace ReportExpert.Core.Shell;

public enum DockTarget
{
    Left,
    Right,
    Bottom
}

public interface IShellBuilder
{
    void RegisterSidebar(string id, Type viewModelType, string iconKey, int order, bool isEnabled = true);
    void RegisterToolPanel(string id, Type viewModelType, DockTarget target, int order);
    void RegisterStatusBarProvider(IStatusBarProvider provider);
    void RegisterCommand(ICommandDescriptor descriptor);
}

public interface ICommandDescriptor
{
    string Id { get; }
    string DisplayName { get; }
    string? IconKey { get; }
}

public interface IStatusBarProvider
{
    string Id { get; }
    object? GetStatusContent();
}
