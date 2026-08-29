using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using TextMateSharp.Themes;
using FontStyle = TextMateSharp.Themes.FontStyle;

namespace ReportExpert.Editors.TextMate;

public abstract class TextTransformation : TextSegment
{
    public abstract void Transform(GenericLineTransformer transformer, DocumentLine line);
}

public class ForegroundTextTransformation : TextTransformation
{
    public Dictionary<int, Brush>? ColorMap { get; set; }
    public Action<Exception>? ExceptionHandler { get; set; }
    public int ForegroundColor { get; set; }
    public int BackgroundColor { get; set; }
    public FontStyle FontStyle { get; set; }

    public override void Transform(GenericLineTransformer transformer, DocumentLine line)
    {
        try
        {
            if (Length == 0)
            {
                return;
            }

            var formattedOffset = 0;
            var endOffset = line.EndOffset;

            if (StartOffset > line.Offset)
            {
                formattedOffset = StartOffset - line.Offset;
            }

            if (EndOffset < line.EndOffset)
            {
                endOffset = EndOffset;
            }

            transformer.SetTextStyle(
                line,
                formattedOffset,
                endOffset - line.Offset - formattedOffset,
                GetBrush(ForegroundColor),
                GetBrush(BackgroundColor),
                GetWpfFontStyle(),
                GetWpfFontWeight(),
                IsUnderline());
        }
        catch (Exception ex)
        {
            ExceptionHandler?.Invoke(ex);
        }
    }

    private System.Windows.FontStyle GetWpfFontStyle()
    {
        if (FontStyle != FontStyle.NotSet &&
            (FontStyle & FontStyle.Italic) != 0)
        {
            return System.Windows.FontStyles.Italic;
        }

        return System.Windows.FontStyles.Normal;
    }

    private System.Windows.FontWeight GetWpfFontWeight()
    {
        if (FontStyle != FontStyle.NotSet &&
            (FontStyle & FontStyle.Bold) != 0)
        {
            return FontWeights.Bold;
        }

        return FontWeights.Regular;
    }

    private bool IsUnderline()
    {
        return FontStyle != FontStyle.NotSet &&
               (FontStyle & FontStyle.Underline) != 0;
    }

    private Brush? GetBrush(int colorId)
    {
        if (ColorMap == null)
        {
            return null;
        }

        return ColorMap.TryGetValue(colorId, out var result) ? result : null;
    }
}
