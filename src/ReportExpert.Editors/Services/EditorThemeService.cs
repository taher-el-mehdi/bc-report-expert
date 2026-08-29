using System.IO;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ReportExpert.Core.Editors;
using ReportExpert.Editors.TextMate;
using TextMateSharp.Grammars;
using Wpf.Ui.Appearance;

namespace ReportExpert.Editors.Services;

public sealed class EditorThemeService : IEditorThemeService
{
    public event EventHandler? ThemeChanged;

    public bool IsDarkTheme { get; private set; }

    public ThemeName GetThemeName() =>
        IsDarkTheme ? ThemeName.DarkPlus : ThemeName.LightPlus;

    public void NotifyThemeChanged()
    {
        IsDarkTheme = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyEditorChrome(TextEditor editor, TextMateInstaller.Installation installation)
    {
        NotifyThemeChanged();
        var registry = new RegistryOptions(GetThemeName());
        installation.SetTheme(registry.GetDefaultTheme());

        if (installation.TryGetThemeColor("editor.background", out string? background) &&
            !string.IsNullOrWhiteSpace(background))
        {
            editor.Background = new SolidColorBrush(TextMateColorHelper.Parse(background));
        }

        if (installation.TryGetThemeColor("editor.foreground", out string? foreground) &&
            !string.IsNullOrWhiteSpace(foreground))
        {
            editor.Foreground = new SolidColorBrush(TextMateColorHelper.Parse(foreground));
        }

        editor.TextArea.SelectionBrush = new SolidColorBrush(
            IsDarkTheme
                ? Color.FromArgb(64, 38, 79, 120)
                : Color.FromArgb(64, 173, 214, 255));

        editor.TextArea.SelectionForeground = editor.Foreground;

        if (installation.TryGetThemeColor("editor.lineHighlightBackground", out string? lineHighlight) &&
            !string.IsNullOrWhiteSpace(lineHighlight))
        {
            editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
                TextMateColorHelper.Parse(lineHighlight));
        }
        else
        {
            editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
                IsDarkTheme
                    ? Color.FromArgb(32, 255, 255, 255)
                    : Color.FromArgb(32, 0, 0, 0));
        }
    }
}
