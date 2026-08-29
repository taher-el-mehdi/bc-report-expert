using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReportExpert.Modules.Copilot.Helpers;

/// <summary>
/// Builds a lightweight chat-oriented <see cref="FlowDocument"/> from common Markdown.
/// Supports bold, italic, inline code, fenced code, headings, and bullet/numbered lists.
/// </summary>
public static partial class MarkdownFlowDocumentBuilder
{
    private static readonly Brush CodeBackground = new SolidColorBrush(Color.FromArgb(0x28, 0x80, 0x80, 0x80));
    private static readonly Brush CodeForeground = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
    private static readonly Thickness ParagraphMargin = new(0, 0, 0, 6);
    private static readonly Thickness ListItemMargin = new(0, 0, 0, 2);

    static MarkdownFlowDocumentBuilder()
    {
        CodeBackground.Freeze();
        CodeForeground.Freeze();
    }

    /// <summary>Creates a FlowDocument suitable for a read-only chat message viewer.</summary>
    public static FlowDocument Build(string? markdown, double fontSize = 13)
    {
        var doc = new FlowDocument
        {
            FontSize = fontSize,
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            doc.Blocks.Add(new Paragraph(new Run(string.Empty)) { Margin = new Thickness(0) });
            return doc;
        }

        string text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                i = AppendCodeBlock(doc, lines, i);
                continue;
            }

            if (TryMatchHeading(line, out int level, out string headingText))
            {
                AppendHeading(doc, headingText, level, fontSize);
                i++;
                continue;
            }

            if (TryMatchBullet(line, out string bulletText))
            {
                i = AppendList(doc, lines, i, ordered: false, bulletText);
                continue;
            }

            if (TryMatchOrdered(line, out string orderedText))
            {
                i = AppendList(doc, lines, i, ordered: true, orderedText);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            var paragraph = new Paragraph { Margin = ParagraphMargin, LineHeight = fontSize * 1.35 };
            AppendInlines(paragraph.Inlines, line);
            doc.Blocks.Add(paragraph);
            i++;
        }

        if (doc.Blocks.Count == 0)
            doc.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0) });

        // Tighten the last block so bubbles don't look padded at the bottom.
        if (doc.Blocks.LastBlock is Paragraph last)
            last.Margin = new Thickness(0);

        return doc;
    }

    private static int AppendCodeBlock(FlowDocument doc, string[] lines, int start)
    {
        int i = start + 1;
        var code = new System.Text.StringBuilder();
        while (i < lines.Length && !lines[i].StartsWith("```", StringComparison.Ordinal))
        {
            if (code.Length > 0)
                code.Append('\n');
            code.Append(lines[i]);
            i++;
        }

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 8),
            Padding = new Thickness(10, 8, 10, 8),
            Background = CodeBackground,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            LineHeight = 16,
        };
        paragraph.Inlines.Add(new Run(code.ToString()) { Foreground = CodeForeground });
        doc.Blocks.Add(paragraph);
        return i < lines.Length ? i + 1 : i;
    }

    private static int AppendList(
        FlowDocument doc,
        string[] lines,
        int start,
        bool ordered,
        string firstItemText)
    {
        var list = new List
        {
            MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(4, 2, 0, 8),
            Padding = new Thickness(14, 0, 0, 0),
        };

        void AddItem(string itemText)
        {
            var item = new ListItem(new Paragraph { Margin = ListItemMargin, LineHeight = 18 });
            AppendInlines(((Paragraph)item.Blocks.FirstBlock).Inlines, itemText);
            list.ListItems.Add(item);
        }

        AddItem(firstItemText);
        int i = start + 1;
        while (i < lines.Length)
        {
            if (ordered)
            {
                if (!TryMatchOrdered(lines[i], out string next))
                    break;
                AddItem(next);
            }
            else
            {
                if (!TryMatchBullet(lines[i], out string next))
                    break;
                AddItem(next);
            }

            i++;
        }

        doc.Blocks.Add(list);
        return i;
    }

    private static void AppendHeading(FlowDocument doc, string text, int level, double baseSize)
    {
        double size = level switch
        {
            1 => baseSize + 4,
            2 => baseSize + 2,
            _ => baseSize + 1,
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 6),
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            LineHeight = size * 1.3,
        };
        AppendInlines(paragraph.Inlines, text);
        doc.Blocks.Add(paragraph);
    }

    private static void AppendInlines(InlineCollection inlines, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        int index = 0;
        foreach (Match match in InlineMarkdownRegex().Matches(text))
        {
            if (match.Index > index)
                inlines.Add(new Run(text[index..match.Index]));

            if (match.Groups["bold"].Success)
            {
                inlines.Add(new Run(match.Groups["bold"].Value) { FontWeight = FontWeights.SemiBold });
            }
            else if (match.Groups["italic"].Success)
            {
                inlines.Add(new Run(match.Groups["italic"].Value) { FontStyle = FontStyles.Italic });
            }
            else if (match.Groups["code"].Success)
            {
                inlines.Add(new Run(match.Groups["code"].Value)
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Background = CodeBackground,
                    Foreground = CodeForeground,
                });
            }

            index = match.Index + match.Length;
        }

        if (index < text.Length)
            inlines.Add(new Run(text[index..]));
    }

    private static bool TryMatchHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var match = HeadingRegex().Match(line);
        if (!match.Success)
            return false;

        level = match.Groups[1].Length;
        text = match.Groups[2].Value.Trim();
        return text.Length > 0;
    }

    private static bool TryMatchBullet(string line, out string text)
    {
        text = string.Empty;
        var match = BulletRegex().Match(line);
        if (!match.Success)
            return false;

        text = match.Groups[1].Value.Trim();
        return true;
    }

    private static bool TryMatchOrdered(string line, out string text)
    {
        text = string.Empty;
        var match = OrderedRegex().Match(line);
        if (!match.Success)
            return false;

        text = match.Groups[1].Value.Trim();
        return true;
    }

    [GeneratedRegex(@"^(#{1,3})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*•]\s+(.+)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^\s*\d+[.)]\s+(.+)$")]
    private static partial Regex OrderedRegex();

    // Bold before italic so **a** wins over *a*; code last among equals via alternation order.
    [GeneratedRegex(@"(\*\*(?<bold>.+?)\*\*|__(?<bold>.+?)__|(?<!\*)\*(?<italic>[^*]+?)\*(?!\*)|(?<!_)_(?<italic>[^_]+?)_(?!_)|`(?<code>[^`]+?)`)")]
    private static partial Regex InlineMarkdownRegex();
}
