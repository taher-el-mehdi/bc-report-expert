using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Wpf.Ui.Appearance;

namespace ReportExpert.RdlcDesigner.Theming;

/// <summary>Applies light/dark chrome and AvalonEdit XML colors for the layout designer.</summary>
internal static class DesignerTheme
{
    public static bool IsDark =>
        ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    public static void ApplyXmlEditor(TextEditor editor)
    {
        bool dark = IsDark;
        IHighlightingDefinition? xml = HighlightingManager.Instance.GetDefinition("XML");
        if (xml is not null)
            ApplyXmlHighlightColors(xml, dark);

        if (dark)
        {
            editor.Background = Brush(0x1E, 0x1E, 0x1E);
            editor.Foreground = Brush(0xD4, 0xD4, 0xD4);
            editor.LineNumbersForeground = Brush(0x85, 0x85, 0x85);
            editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x26, 0x4F, 0x78));
            editor.TextArea.TextView.CurrentLineBackground =
                new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            editor.Background = Brush(0xFA, 0xFA, 0xF8);
            editor.Foreground = Brush(0x1E, 0x1E, 0x1E);
            editor.LineNumbersForeground = Brush(0x88, 0x88, 0x88);
            editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xAD, 0xD6, 0xFF));
            editor.TextArea.TextView.CurrentLineBackground =
                new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x00, 0x00));
        }

        editor.TextArea.SelectionForeground = editor.Foreground;
        editor.TextArea.TextView.Redraw();
    }

    public static Brush DesignDeskBackground =>
        IsDark ? Brush(0x2B, 0x2B, 0x2B) : Brush(0xC8, 0xC8, 0xC8);

    public static Brush MutedForeground =>
        IsDark ? Brush(0xA0, 0xA0, 0xA0) : Brush(0x88, 0x88, 0x88);

    public static Brush PrimaryForeground =>
        IsDark ? Brush(0xF0, 0xF0, 0xF0) : Brush(0x22, 0x22, 0x22);

    public static Brush SecondaryForeground =>
        IsDark ? Brush(0xC8, 0xC8, 0xC8) : Brush(0x55, 0x55, 0x55);

    public static Brush ToolboxHoverBackground =>
        IsDark ? Brush(0x2A, 0x3A, 0x52) : Brush(0xE8, 0xF0, 0xFE);

    public static Brush ToolboxHoverBorder =>
        IsDark ? Brush(0x3D, 0x5A, 0x80) : Brush(0xC2, 0xD5, 0xF2);

    public static Brush ToolboxActiveBackground =>
        IsDark ? Brush(0x1E, 0x3A, 0x5F) : Brush(0xD0, 0xE2, 0xFF);

    public static Brush ToolboxActiveBorder =>
        IsDark ? Brush(0x4C, 0x8A, 0xD9) : Brush(0x7A, 0xA2, 0xE8);

    public static Brush StatusOk => Brush(0x2E, 0x7D, 0x32);
    public static Brush StatusOkDark => Brush(0x81, 0xC7, 0x84);
    public static Brush StatusError => Brush(0xC6, 0x28, 0x28);
    public static Brush StatusErrorDark => Brush(0xEF, 0x9A, 0x9A);

    public static Brush StatusSuccessForeground => IsDark ? StatusOkDark : StatusOk;
    public static Brush StatusErrorForeground => IsDark ? StatusErrorDark : StatusError;

    public static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static void ApplyXmlHighlightColors(IHighlightingDefinition xml, bool dark)
    {
        if (dark)
        {
            SetColor(xml, "XmlTag", "#569CD6");
            SetColor(xml, "AttributeName", "#9CDCFE");
            SetColor(xml, "AttributeValue", "#CE9178");
            SetColor(xml, "Comment", "#6A9955");
            SetColor(xml, "CData", "#D4D4D4");
            SetColor(xml, "DocType", "#808080");
            SetColor(xml, "XmlDeclaration", "#569CD6");
            SetColor(xml, "Entity", "#DCDCAA");
            SetColor(xml, "BrokenEntity", "#F44747");
        }
        else
        {
            // AvalonEdit default-ish light XML palette
            SetColor(xml, "XmlTag", "#A31515");
            SetColor(xml, "AttributeName", "#FF0000");
            SetColor(xml, "AttributeValue", "#0000FF");
            SetColor(xml, "Comment", "#008000");
            SetColor(xml, "CData", "#808080");
            SetColor(xml, "DocType", "#808080");
            SetColor(xml, "XmlDeclaration", "#808080");
            SetColor(xml, "Entity", "#FF0000");
            SetColor(xml, "BrokenEntity", "#FF0000");
        }
    }

    private static void SetColor(IHighlightingDefinition definition, string name, string hex)
    {
        HighlightingColor? color = definition.GetNamedColor(name);
        if (color is null)
            return;

        color.Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }
}
