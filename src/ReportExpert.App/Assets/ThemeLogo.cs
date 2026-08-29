using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReportExpert.Editors.Editors;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WpfImage = System.Windows.Controls.Image;

namespace ReportExpert.App.Assets;

/// <summary>Swaps logo assets between light- and dark-theme transparent PNGs.</summary>
public static class ThemeLogo
{
    private const string LightLogo = "pack://application:,,,/Assets/logo-light.png";
    private const string DarkLogo = "pack://application:,,,/Assets/logo-dark.png";

    public static void Bind(WpfImage target)
    {
        Update(target);
        Subscribe(() => Update(target));
    }

    public static void Bind(ImageIcon target)
    {
        Update(target);
        Subscribe(() => Update(target));
    }

    public static void Update(WpfImage target) => target.Source = CurrentSource();

    public static void Update(ImageIcon target) => target.Source = CurrentSource();

    public static ImageSource CurrentSource()
    {
        bool isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
        string uri = isDark ? DarkLogo : LightLogo;
        return new BitmapImage(new Uri(uri, UriKind.Absolute));
    }

    private static void Subscribe(Action update)
    {
        if (EditorServicesLocator.ThemeService is { } themeService)
            themeService.ThemeChanged += (_, _) => update();
    }
}
