using Microsoft.Extensions.DependencyInjection;
using ReportExpert.Core.Configuration;
using ReportExpert.Core.Editors;
using ReportExpert.Core.Services;
using ReportExpert.Editors.Services;
using ReportExpert.Infrastructure.Configuration;
using ReportExpert.Infrastructure.Services;

namespace ReportExpert.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAlSourceLoadService, AlSourceLoadService>();
        services.AddSingleton<IEditorThemeService, EditorThemeService>();
        services.AddSingleton<IAlSyntaxHighlightingService, AlSyntaxHighlightingService>();
        services.AddSingleton<IAlSourcePathResolver, AlSourcePathResolver>();
        return services;
    }
}
