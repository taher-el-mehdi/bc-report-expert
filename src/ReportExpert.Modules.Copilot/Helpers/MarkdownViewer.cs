using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ReportExpert.Modules.Copilot.Helpers;

/// <summary>
/// Read-only markdown chat body. Looks like text, not an input field, while still allowing
/// selection and copy (VS Code agent-style).
/// </summary>
public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownViewer),
        new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsDocumentEnabled = true;
        AcceptsReturn = false;
        AcceptsTab = false;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        Margin = new Thickness(0);
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = true;
        Cursor = Cursors.IBeam;
        CaretBrush = Brushes.Transparent;
        SelectionBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x37, 0x99, 0xE8));
        IsUndoEnabled = false;
        UndoLimit = 0;
        AutoWordSelection = true;

        // Avoid the TextBox chrome / focus glow so this never reads as the composer.
        SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

        Document = MarkdownFlowDocumentBuilder.Build(string.Empty);
    }

    /// <summary>Markdown source rendered into the document.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // RichTextBox collapses to a tiny width when the parent sizes to content
        // (Left-aligned chat bubbles). Prefer the available width so wrapping is sane.
        if (Document is not null &&
            !double.IsInfinity(constraint.Width) &&
            constraint.Width > 0)
        {
            Document.PageWidth = Math.Max(constraint.Width - 4, 80);
        }

        return base.MeasureOverride(constraint);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdatePageWidth();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        // Keep caret invisible — selection still works for copy.
        CaretBrush = Brushes.Transparent;
    }

    private void UpdatePageWidth()
    {
        if (Document is null || ActualWidth <= 0)
            return;

        Document.PageWidth = Math.Max(ActualWidth - 4, 80);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkdownViewer viewer)
            return;

        double fontSize = viewer.FontSize > 0 ? viewer.FontSize : 13;
        viewer.Document = MarkdownFlowDocumentBuilder.Build(e.NewValue as string, fontSize);
        viewer.UpdatePageWidth();
    }
}
