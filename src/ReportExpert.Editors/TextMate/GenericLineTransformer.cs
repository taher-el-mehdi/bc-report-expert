using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace ReportExpert.Editors.TextMate;

public abstract class GenericLineTransformer : DocumentColorizingTransformer
{
    private readonly Action<Exception>? _exceptionHandler;

    protected GenericLineTransformer(Action<Exception>? exceptionHandler)
    {
        _exceptionHandler = exceptionHandler;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        try
        {
            TransformLine(line, CurrentContext);
        }
        catch (Exception ex)
        {
            _exceptionHandler?.Invoke(ex);
        }
    }

    protected abstract void TransformLine(DocumentLine line, ITextRunConstructionContext context);

    public void SetTextStyle(
        DocumentLine line,
        int startIndex,
        int length,
        Brush? foreground,
        Brush? background,
        System.Windows.FontStyle fontStyle,
        System.Windows.FontWeight fontWeight,
        bool isUnderline)
    {
        int startOffset;
        int endOffset;

        if (startIndex >= 0 && length > 0)
        {
            if (line.Offset + startIndex + length > line.EndOffset)
            {
                length = line.EndOffset - startIndex - line.Offset - startIndex;
            }

            startOffset = line.Offset + startIndex;
            endOffset = line.Offset + startIndex + length;
        }
        else
        {
            startOffset = line.Offset;
            endOffset = line.EndOffset;
        }

        if (startOffset > CurrentContext.Document.TextLength ||
            endOffset > CurrentContext.Document.TextLength)
        {
            return;
        }

        ChangeLinePart(
            startOffset,
            endOffset,
            visualLine => ChangeVisualLine(visualLine, foreground, background, fontStyle, fontWeight, isUnderline));
    }

    private static void ChangeVisualLine(
        VisualLineElement visualLine,
        Brush? foreground,
        Brush? background,
        System.Windows.FontStyle fontStyle,
        System.Windows.FontWeight fontWeight,
        bool isUnderline)
    {
        if (foreground != null)
        {
            visualLine.TextRunProperties.SetForegroundBrush(foreground);
        }

        if (background != null)
        {
            visualLine.TextRunProperties.SetBackgroundBrush(background);
        }

        if (isUnderline)
        {
            visualLine.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
        }

        if (visualLine.TextRunProperties.Typeface.Style != fontStyle ||
            visualLine.TextRunProperties.Typeface.Weight != fontWeight)
        {
            visualLine.TextRunProperties.SetTypeface(new Typeface(
                visualLine.TextRunProperties.Typeface.FontFamily,
                fontStyle,
                fontWeight,
                visualLine.TextRunProperties.Typeface.Stretch));
        }
    }
}
