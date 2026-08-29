using ReportExpert.Editors.Editors;
using ReportExpert.Editors.Services;

namespace ReportExpert.App.Shell;

/// <summary>Initializes shared editor services for Preview and related modules.</summary>
public static class EditorServiceBootstrap
{
    public static void Initialize()
    {
        var themeService = new EditorThemeService();
        var loadService = new AlSourceLoadService();
        var syntaxService = new AlSyntaxHighlightingService(themeService);
        var pathResolver = new AlSourcePathResolver();

        EditorServicesLocator.ThemeService = themeService;
        EditorServicesLocator.LoadService = loadService;
        EditorServicesLocator.SyntaxHighlighting = syntaxService;
        EditorServicesLocator.PathResolver = pathResolver;

        themeService.NotifyThemeChanged();
    }
}
