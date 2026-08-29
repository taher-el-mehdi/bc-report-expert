using ReportExpert.Common;
using ReportExpert.Core.Configuration;
using ReportExpert.Core.Services;
using Wpf.Ui.Appearance;

namespace ReportExpert.Infrastructure.Services;

/// <summary>
/// Applies WPF-UI themes from settings (scaffolding path).
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;

    public ThemeService(ISettingsService settingsService) => _settingsService = settingsService;

    public void ApplyTheme(string? theme = null)
    {
        theme ??= _settingsService.Settings.Theme;

        if (theme == ThemeNames.System)
        {
            ApplicationThemeManager.ApplySystemTheme();
            return;
        }

        ApplicationTheme appTheme = theme == ThemeNames.Dark
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(appTheme);
    }
}
