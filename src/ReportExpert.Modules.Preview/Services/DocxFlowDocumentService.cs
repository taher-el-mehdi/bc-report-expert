using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfParagraph = System.Windows.Documents.Paragraph;
using WpfRun = System.Windows.Documents.Run;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;
using WpfTextAlignment = System.Windows.TextAlignment;
using WordBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
using WordHyperlink = DocumentFormat.OpenXml.Wordprocessing.Hyperlink;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WordTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WordTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WordTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Builds a free, license-free WPF <see cref="FlowDocument"/> preview of a .docx
/// using the Open XML SDK (no commercial Word control required).
/// </summary>
public sealed class DocxFlowDocumentService
{
    private static readonly SolidColorBrush ContentControlBrush = CreateFrozenBrush(0xE8, 0xF2, 0xFC);
    private static readonly SolidColorBrush ContentControlBorder = CreateFrozenBrush(0x6B, 0x9B, 0xD1);
    private static readonly SolidColorBrush TableBorderBrush = CreateFrozenBrush(0xC0, 0xC0, 0xC0);

    public FlowDocument Load(string path) => LoadWithSummary(path).Document;

    public (FlowDocument Document, string Summary) LoadWithSummary(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Word layout file not found.", path);

        using var doc = WordprocessingDocument.Open(path, false);
        Body? body = doc.MainDocumentPart?.Document?.Body;
        var flow = CreateBaseDocument();

        int paragraphs = 0;
        int tables = 0;
        int contentControls = 0;
        int blockCount = 0;

        if (body is not null)
        {
            paragraphs = body.Elements<WordParagraph>().Count();
            tables = body.Descendants<WordTable>().Count();
            contentControls = body.Descendants<SdtElement>().Count();

            foreach (var child in body.ChildElements)
            {
                switch (child)
                {
                    case WordParagraph paragraph:
                        flow.Blocks.Add(ConvertParagraph(paragraph));
                        blockCount++;
                        break;
                    case WordTable table:
                        flow.Blocks.Add(ConvertTable(table));
                        blockCount++;
                        break;
                    case SdtElement sdt:
                        foreach (var block in ConvertSdtBlocks(sdt))
                        {
                            flow.Blocks.Add(block);
                            blockCount++;
                        }
                        break;
                }
            }
        }

        if (blockCount == 0)
        {
            flow.Blocks.Add(new WpfParagraph(new WpfRun(
                "(No visible text in this Word layout — it may be template XML / content controls only.)")));
        }

        string summary = $"{paragraphs} paragraph(s)";
        if (tables > 0)
            summary += $" · {tables} table(s)";
        if (contentControls > 0)
            summary += $" · {contentControls} content control(s)";

        return (flow, summary);
    }

    private static FlowDocument CreateBaseDocument() =>
        new()
        {
            FontFamily = new WpfFontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(48, 36, 48, 36),
            Background = WpfBrushes.White,
            Foreground = WpfBrushes.Black,
            ColumnWidth = double.PositiveInfinity
        };

    private static IEnumerable<Block> ConvertSdtBlocks(SdtElement sdt)
    {
        string label = GetContentControlLabel(sdt);
        OpenXmlElement? content = sdt.Descendants<SdtContentBlock>().FirstOrDefault()
            ?? (OpenXmlElement?)sdt.Descendants<SdtContentRun>().FirstOrDefault();

        bool yielded = false;
        if (content is not null)
        {
            foreach (var child in content.ChildElements)
            {
                if (child is WordParagraph paragraph)
                {
                    var p = ConvertParagraph(paragraph);
                    PrefixedContentControl(p, label);
                    yield return p;
                    yielded = true;
                }
                else if (child is WordTable table)
                {
                    yield return ConvertTable(table);
                    yielded = true;
                }
            }
        }

        if (!yielded)
        {
            var placeholder = new WpfParagraph();
            PrefixedContentControl(placeholder, label);
            if (placeholder.Inlines.Count == 0)
                placeholder.Inlines.Add(CreateContentControlRun(label));
            yield return placeholder;
        }
    }

    private static void PrefixedContentControl(WpfParagraph paragraph, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var prefix = CreateContentControlRun(
            paragraph.Inlines.Count == 0 ? $"[{label}]" : $"[{label}] · ");

        // Rebuild instead of InsertBefore — WPF InlineCollection throws on some Open XML-derived trees.
        var existing = new List<Inline>();
        while (paragraph.Inlines.FirstInline is { } inline)
        {
            paragraph.Inlines.Remove(inline);
            existing.Add(inline);
        }

        paragraph.Inlines.Add(prefix);
        foreach (Inline inline in existing)
            paragraph.Inlines.Add(inline);
    }

    private static WpfRun CreateContentControlRun(string label) =>
        new(label.StartsWith('[') ? label : $"[{label}]")
        {
            Background = ContentControlBrush,
            Foreground = ContentControlBorder,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        };

    private static string GetContentControlLabel(SdtElement sdt)
    {
        string? alias = sdt.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(alias))
            return alias;

        string? tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(tag))
            return tag;

        return "Content control";
    }

    private static WpfParagraph ConvertParagraph(WordParagraph paragraph)
    {
        var result = new WpfParagraph
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        ApplyParagraphAlignment(paragraph, result);

        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                case WordRun run:
                    AppendRun(result, run);
                    break;
                case SdtElement sdt:
                    foreach (var inlineRun in sdt.Descendants<WordRun>())
                        AppendRun(result, inlineRun);
                    string label = GetContentControlLabel(sdt);
                    if (!string.IsNullOrWhiteSpace(label) &&
                        !result.Inlines.OfType<WpfRun>().Any(r => r.Text.Contains(label, StringComparison.Ordinal)))
                    {
                        result.Inlines.Add(CreateContentControlRun(label));
                    }
                    break;
                case WordHyperlink hyperlink:
                    foreach (var run in hyperlink.Elements<WordRun>())
                        AppendRun(result, run);
                    break;
            }
        }

        if (result.Inlines.Count == 0)
            result.Inlines.Add(new WpfRun(string.Empty));

        return result;
    }

    private static void ApplyParagraphAlignment(WordParagraph paragraph, WpfParagraph result)
    {
        EnumValue<JustificationValues>? jc = paragraph.ParagraphProperties?.Justification?.Val;
        if (jc?.HasValue != true)
            return;

        string? value = jc.InnerText;
        result.TextAlignment = value switch
        {
            "center" => WpfTextAlignment.Center,
            "right" => WpfTextAlignment.Right,
            "both" => WpfTextAlignment.Justify,
            _ => WpfTextAlignment.Left
        };
    }

    private static void AppendRun(WpfParagraph paragraph, WordRun run)
    {
        var props = run.RunProperties;

        foreach (var child in run.ChildElements)
        {
            if (child is Text text && !string.IsNullOrEmpty(text.Text))
            {
                paragraph.Inlines.Add(CreateStyledRun(text.Text, props));
            }
            else if (child is WordBreak or CarriageReturn)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            else if (child is TabChar)
            {
                paragraph.Inlines.Add(new WpfRun("\t"));
            }
        }
    }

    private static WpfRun CreateStyledRun(string text, RunProperties? props)
    {
        var run = new WpfRun(text);

        if (props is null)
            return run;

        if (props.Bold is not null || props.BoldComplexScript is not null)
            run.FontWeight = FontWeights.Bold;

        if (props.Italic is not null || props.ItalicComplexScript is not null)
            run.FontStyle = FontStyles.Italic;

        if (props.Underline is not null &&
            props.Underline.Val?.InnerText is not ("none" or null or ""))
            run.TextDecorations = TextDecorations.Underline;

        if (props.Strike is not null)
            run.TextDecorations = TextDecorations.Strikethrough;

        if (props.FontSize?.Val?.Value is { } sizeVal &&
            double.TryParse(sizeVal, out double halfPoints) &&
            halfPoints > 0)
        {
            run.FontSize = halfPoints / 2.0;
        }

        string? font = props.RunFonts?.Ascii?.Value ?? props.RunFonts?.HighAnsi?.Value;
        if (!string.IsNullOrWhiteSpace(font))
            run.FontFamily = new WpfFontFamily(font);

        if (TryParseHexColor(props.Color?.Val?.Value, out WpfColor color))
            run.Foreground = new SolidColorBrush(color);

        string? shading = props.Shading?.Fill?.Value;
        if (TryParseHexColor(shading, out WpfColor bg))
            run.Background = new SolidColorBrush(bg);

        return run;
    }

    private static WpfTable ConvertTable(WordTable table)
    {
        var result = new WpfTable
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1)
        };

        var rowGroup = new TableRowGroup();
        result.RowGroups.Add(rowGroup);

        int maxColumns = 1;
        foreach (WordTableRow row in table.Elements<WordTableRow>())
        {
            var wpfRow = new WpfTableRow();
            int colCount = 0;
            foreach (WordTableCell cell in row.Elements<WordTableCell>())
            {
                var wpfCell = new WpfTableCell
                {
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 4, 6, 4)
                };

                foreach (WordParagraph paragraph in cell.Elements<WordParagraph>())
                    wpfCell.Blocks.Add(ConvertParagraph(paragraph));

                if (wpfCell.Blocks.Count == 0)
                    wpfCell.Blocks.Add(new WpfParagraph(new WpfRun(string.Empty)));

                wpfRow.Cells.Add(wpfCell);
                colCount++;
            }

            maxColumns = Math.Max(maxColumns, colCount);
            rowGroup.Rows.Add(wpfRow);
        }

        for (int i = 0; i < maxColumns; i++)
            result.Columns.Add(new TableColumn());

        return result;
    }

    private static bool TryParseHexColor(string? value, out WpfColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return false;

        string hex = value.TrimStart('#');
        if (hex.Length != 6)
            return false;

        try
        {
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            color = WpfColor.FromRgb(r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(WpfColor.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
