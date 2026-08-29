namespace ReportExpert.Core.Services;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string description, string? solution = null, Exception? exception = null);
    void ShowInfo(string message, string title = "Report Expert");
    Task<string?> ShowOpenFileDialogAsync(string filter, string title);
    Task<string?> ShowSaveFileDialogAsync(string filter, string defaultFileName, string? initialDirectory);
    Task<string?> ShowFolderBrowserDialogAsync(string description, string? selectedPath);
}

public interface IThemeService
{
    void ApplyTheme(string theme);
}

public interface INavigationService
{
    void NavigateToSidebar(string sidebarId);
}
