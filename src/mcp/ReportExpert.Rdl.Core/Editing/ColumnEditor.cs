using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Structural and cosmetic edits to the columns of a tablix.
/// </summary>
/// <remarks>
/// A tablix column exists in three places at once: a <c>TablixColumn</c> width definition, a
/// <c>TablixMember</c> in the column hierarchy, and one cell in every row. Every operation here
/// keeps all three in step, because a mismatch renders as a corrupt report rather than a
/// misaligned one.
/// </remarks>
public static class ColumnEditor
{
    /// <summary>
    /// Inserts a new column into a tablix.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="columnIndex">
    /// Zero-based position to insert at, from <c>0</c> to the current column count. Pass <c>-1</c>
    /// to append.
    /// </param>
    /// <param name="headerText">Static text for the header row cell.</param>
    /// <param name="fieldBinding">
    /// The detail row expression, for example <c>=Fields!Amount.Value</c>.
    /// </param>
    /// <param name="width">Column width as an RDL size string.</param>
    /// <param name="formatString">Optional format string for the detail cell, for example <c>C2</c>.</param>
    /// <param name="footerExpression">Optional expression for the totals row cell.</param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException"><paramref name="columnIndex"/> or <paramref name="width"/> is invalid.</exception>
    /// <exception cref="RdlNotFoundException">The tablix does not exist.</exception>
    public static EditOutcome AddColumn(
        RdlDocument document,
        int columnIndex,
        string headerText,
        string fieldBinding,
        string width = "1in",
        string? formatString = null,
        string? footerExpression = null,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(headerText);
        ArgumentNullException.ThrowIfNull(fieldBinding);
        Dimension.Validate(width, nameof(width));

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);
        var container = TablixNavigator.RequireColumnsContainer(tablix, ns);

        var existing = TablixNavigator.Columns(tablix, ns);
        int columnCount = existing.Count;

        int target = columnIndex == -1 ? columnCount : columnIndex;
        if (target < 0 || target > columnCount)
        {
            throw new ArgumentException(
                $"Invalid column index {columnIndex}. Must be 0-{columnCount}, or -1 to append.",
                nameof(columnIndex));
        }

        var definition = new XElement(ns + "TablixColumn", new XElement(ns + "Width", width));
        InsertAt(container, ns + "TablixColumn", target, definition);

        var hierarchy = TablixNavigator.ColumnHierarchyMembers(tablix, ns);
        if (hierarchy is not null)
            InsertAt(hierarchy, ns + "TablixMember", target, new XElement(ns + "TablixMember"));

        var rows = TablixNavigator.Rows(tablix, ns);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex].Element(ns + "TablixCells");
            if (cells is null)
                continue;

            var kind = TablixNavigator.DetectRowKind(TablixNavigator.Cells(rows[rowIndex], ns), ns);

            var cell = CreateCell(
                ns, kind, rowIndex, target,
                headerText, fieldBinding, formatString, footerExpression);

            InsertAt(cells, ns + "TablixCell", target, cell);
        }

        TablixNavigator.RecalculateWidth(tablix, ns);

        return new EditOutcome($"Added column \"{headerText}\" at position {target}")
        {
            Details = new Dictionary<string, object?>
            {
                ["column_index"] = target,
                ["column_count"] = columnCount + 1,
            },
        };
    }

    /// <summary>
    /// Removes a column from a tablix.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="columnIndex">Zero-based index of the column to remove.</param>
    /// <param name="autoAdjustPageWidth">
    /// Whether to shrink the page width to the remaining tablix width plus horizontal margins.
    /// </param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException"><paramref name="columnIndex"/> is out of range.</exception>
    public static EditOutcome RemoveColumn(
        RdlDocument document,
        int columnIndex,
        bool autoAdjustPageWidth = true,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);
        TablixNavigator.RequireColumnsContainer(tablix, ns);

        var columns = TablixNavigator.Columns(tablix, ns);
        RequireColumnIndex(columnIndex, columns.Count);

        columns[columnIndex].Remove();

        var hierarchy = TablixNavigator.ColumnHierarchyMembers(tablix, ns);
        if (hierarchy is not null)
        {
            var members = hierarchy.Elements(ns + "TablixMember").ToList();
            if (columnIndex < members.Count)
                members[columnIndex].Remove();
        }

        foreach (var row in TablixNavigator.Rows(tablix, ns))
        {
            var cells = TablixNavigator.Cells(row, ns);
            if (columnIndex < cells.Count)
                cells[columnIndex].Remove();
        }

        double tablixWidth = TablixNavigator.RecalculateWidth(tablix, ns);

        if (autoAdjustPageWidth)
            PageEditor.FitPageWidthTo(document, tablixWidth);

        return new EditOutcome($"Removed column at index {columnIndex}")
        {
            Details = new Dictionary<string, object?>
            {
                ["column_count"] = columns.Count - 1,
                ["tablix_width"] = Dimension.FromInches(tablixWidth),
            },
        };
    }

    /// <summary>
    /// Moves a column to a different position, carrying its width, header, binding and styling.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="fromIndex">Zero-based index of the column to move.</param>
    /// <param name="toIndex">Zero-based index to move it to, in terms of the current layout.</param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException">Either index is out of range.</exception>
    public static EditOutcome MoveColumn(
        RdlDocument document,
        int fromIndex,
        int toIndex,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);
        var container = TablixNavigator.RequireColumnsContainer(tablix, ns);

        var columns = TablixNavigator.Columns(tablix, ns);
        RequireColumnIndex(fromIndex, columns.Count, nameof(fromIndex));
        RequireColumnIndex(toIndex, columns.Count, nameof(toIndex));

        if (fromIndex == toIndex)
            return new EditOutcome($"Column {fromIndex} is already at position {toIndex}; nothing to do");

        Relocate(container, ns + "TablixColumn", fromIndex, toIndex);

        var hierarchy = TablixNavigator.ColumnHierarchyMembers(tablix, ns);
        if (hierarchy is not null)
            Relocate(hierarchy, ns + "TablixMember", fromIndex, toIndex);

        foreach (var row in TablixNavigator.Rows(tablix, ns))
        {
            var cells = row.Element(ns + "TablixCells");
            if (cells is not null)
                Relocate(cells, ns + "TablixCell", fromIndex, toIndex);
        }

        return new EditOutcome($"Moved column from index {fromIndex} to {toIndex}");
    }

    /// <summary>
    /// Changes the width of a column and recalculates the tablix total.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="columnIndex">Zero-based index of the column.</param>
    /// <param name="newWidth">The new width as an RDL size string, for example <c>"2.5in"</c>.</param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException">The index is out of range or the width is malformed.</exception>
    public static EditOutcome UpdateColumnWidth(
        RdlDocument document,
        int columnIndex,
        string newWidth,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        Dimension.Validate(newWidth, nameof(newWidth));

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);

        var columns = TablixNavigator.Columns(tablix, ns);
        RequireColumnIndex(columnIndex, columns.Count);

        var column = columns[columnIndex];
        var widthElement = column.Element(ns + "Width");

        if (widthElement is null)
            column.Add(new XElement(ns + "Width", newWidth));
        else
            widthElement.Value = newWidth;

        double total = TablixNavigator.RecalculateWidth(tablix, ns);

        return new EditOutcome($"Updated column {columnIndex} width to {newWidth}")
        {
            Details = new Dictionary<string, object?> { ["tablix_width"] = Dimension.FromInches(total) },
        };
    }

    /// <summary>
    /// Renames a column header by matching its exact current text.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="oldHeader">The current header text, matched exactly.</param>
    /// <param name="newHeader">The replacement text.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">No value in the report holds that exact text.</exception>
    public static EditOutcome UpdateColumnHeader(RdlDocument document, string oldHeader, string newHeader)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(oldHeader);
        ArgumentNullException.ThrowIfNull(newHeader);

        foreach (var value in document.Descendants("Value"))
        {
            if (!string.Equals(value.Value, oldHeader, StringComparison.Ordinal))
                continue;

            value.Value = newHeader;
            return new EditOutcome($"Updated header from \"{oldHeader}\" to \"{newHeader}\"");
        }

        throw new RdlNotFoundException(
            RdlTarget.Textbox,
            $"Header \"{oldHeader}\" not found. The text must match exactly, including case and spacing.");
    }

    /// <summary>
    /// Sets the format string on a column's detail cell.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="columnIndex">Zero-based index of the column.</param>
    /// <param name="formatString">A .NET format string, for example <c>C2</c> or <c>#,0.00</c>.</param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The tablix has no detail row, or the cell has no text run.</exception>
    public static EditOutcome UpdateColumnFormat(
        RdlDocument document,
        int columnIndex,
        string formatString,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(formatString);

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);

        var dataRow = TablixNavigator.FindRow(tablix, ns, TablixRowKind.Data)
            ?? throw new RdlNotFoundException(
                RdlTarget.Row,
                $"Tablix \"{TablixNavigator.NameOf(tablix)}\" has no detail row, so there is no cell to format.");

        var cells = TablixNavigator.Cells(dataRow, ns);
        RequireColumnIndex(columnIndex, cells.Count);

        var textbox = TablixNavigator.FirstTextbox(cells[columnIndex], ns)
            ?? throw new RdlNotFoundException(
                RdlTarget.Textbox,
                $"No textbox found in column {columnIndex} of the detail row.");

        var textRun = textbox.Descendants(ns + "TextRun").FirstOrDefault()
            ?? throw new RdlNotFoundException(
                RdlTarget.Textbox,
                $"The textbox in column {columnIndex} has no TextRun, so it displays nothing to format.");

        var style = GetOrCreate(textRun, ns + "Style");
        GetOrCreate(style, ns + "Format").Value = formatString;

        return new EditOutcome($"Updated format for column {columnIndex} to \"{formatString}\"");
    }

    /// <summary>
    /// Applies visual styling to a column across one or more of its rows.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="columnIndex">Zero-based index of the column.</param>
    /// <param name="style">The styling to apply. Unset properties are left unchanged.</param>
    /// <param name="target">Which rows to apply the styling to.</param>
    /// <param name="tablixName">The tablix to change, or <see langword="null"/> for the first one.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException">The style is empty or the index is out of range.</exception>
    /// <remarks>
    /// Text-level properties are written to the text run's style and cell-level properties to the
    /// textbox's own style, which is where Reporting Services expects to find each of them.
    /// </remarks>
    public static EditOutcome SetColumnStyle(
        RdlDocument document,
        int columnIndex,
        CellStyle style,
        ColumnRowTarget target = ColumnRowTarget.All,
        string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(style);

        if (style.IsEmpty)
            throw new ArgumentException("No style properties were supplied, so there is nothing to apply.", nameof(style));

        XNamespace ns = document.Ns;
        var tablix = TablixNavigator.RequireTablix(document, tablixName);
        var rows = TablixNavigator.Rows(tablix, ns);

        int applied = 0;

        foreach (var row in rows)
        {
            var cells = TablixNavigator.Cells(row, ns);
            if (columnIndex >= cells.Count)
                continue;

            var kind = TablixNavigator.DetectRowKind(cells, ns);
            if (!Matches(kind, target))
                continue;

            var textbox = TablixNavigator.FirstTextbox(cells[columnIndex], ns);
            if (textbox is null)
                continue;

            ApplyStyle(textbox, ns, style);
            applied++;
        }

        if (applied == 0)
        {
            throw new RdlNotFoundException(
                RdlTarget.Column,
                $"No {target.ToString().ToLowerInvariant()} cell was found at column index {columnIndex}. " +
                $"The tablix has {TablixNavigator.Columns(tablix, ns).Count} columns.");
        }

        return new EditOutcome($"Applied styling to {applied} cell(s) in column {columnIndex}")
        {
            Details = new Dictionary<string, object?> { ["cells_updated"] = applied },
        };
    }

    private static bool Matches(TablixRowKind kind, ColumnRowTarget target) => target switch
    {
        ColumnRowTarget.All => kind != TablixRowKind.Empty,
        ColumnRowTarget.Header => kind == TablixRowKind.Header,
        ColumnRowTarget.Data => kind == TablixRowKind.Data,
        ColumnRowTarget.Footer => kind == TablixRowKind.Footer,
        _ => false,
    };

    private static void ApplyStyle(XElement textbox, XNamespace ns, CellStyle style)
    {
        var textRun = textbox.Descendants(ns + "TextRun").FirstOrDefault();

        if (textRun is not null)
        {
            var runStyle = GetOrCreate(textRun, ns + "Style");
            SetIfPresent(runStyle, ns + "FontFamily", style.FontFamily);
            SetIfPresent(runStyle, ns + "FontSize", style.FontSize);
            SetIfPresent(runStyle, ns + "FontWeight", style.FontWeight);
            SetIfPresent(runStyle, ns + "FontStyle", style.FontStyle);
            SetIfPresent(runStyle, ns + "Color", style.Color);
            SetIfPresent(runStyle, ns + "Format", style.Format);
        }

        if (style.BackgroundColor is null && style.TextAlign is null && style.VerticalAlign is null)
            return;

        var boxStyle = GetOrCreate(textbox, ns + "Style");
        SetIfPresent(boxStyle, ns + "BackgroundColor", style.BackgroundColor);
        SetIfPresent(boxStyle, ns + "TextAlign", style.TextAlign);
        SetIfPresent(boxStyle, ns + "VerticalAlign", style.VerticalAlign);
    }

    private static void SetIfPresent(XElement parent, XName name, string? value)
    {
        if (value is null)
            return;

        GetOrCreate(parent, name).Value = value;
    }

    internal static XElement GetOrCreate(XElement parent, XName name)
    {
        var existing = parent.Element(name);
        if (existing is not null)
            return existing;

        var created = new XElement(name);
        parent.Add(created);
        return created;
    }

    /// <summary>
    /// Inserts a child at an ordinal position among its same-named siblings, leaving any other
    /// children of the parent where they are.
    /// </summary>
    private static void InsertAt(XElement parent, XName name, int index, XElement child)
    {
        var siblings = parent.Elements(name).ToList();

        if (index >= siblings.Count)
            parent.Add(child);
        else
            siblings[index].AddBeforeSelf(child);
    }

    private static void Relocate(XElement parent, XName name, int fromIndex, int toIndex)
    {
        var siblings = parent.Elements(name).ToList();
        if (fromIndex >= siblings.Count || toIndex >= siblings.Count)
            return;

        var moving = siblings[fromIndex];
        var anchor = siblings[toIndex];

        moving.Remove();

        // Moving right lands after the anchor, moving left lands before it. Both read as
        // "the column now sits at toIndex" once the source position has been vacated.
        if (fromIndex < toIndex)
            anchor.AddAfterSelf(moving);
        else
            anchor.AddBeforeSelf(moving);
    }

    private static void RequireColumnIndex(int index, int count, string parameterName = "columnIndex")
    {
        if (index < 0 || index >= count)
        {
            throw new ArgumentException(
                count == 0
                    ? $"Invalid column index {index}. The tablix has no columns."
                    : $"Invalid column index {index}. Must be 0-{count - 1}.",
                parameterName);
        }
    }

    private static XElement CreateCell(
        XNamespace ns,
        TablixRowKind kind,
        int rowIndex,
        int columnIndex,
        string headerText,
        string fieldBinding,
        string? formatString,
        string? footerExpression)
    {
        var value = new XElement(ns + "Value");
        var textRun = new XElement(ns + "TextRun", value);

        switch (kind)
        {
            case TablixRowKind.Header:
                value.Value = headerText;
                break;

            case TablixRowKind.Data:
                value.Value = fieldBinding;
                if (!string.IsNullOrEmpty(formatString))
                    textRun.Add(new XElement(ns + "Style", new XElement(ns + "Format", formatString)));
                break;

            case TablixRowKind.Footer:
                value.Value = footerExpression ?? string.Empty;
                break;

            default:
                value.Value = string.Empty;
                break;
        }

        return new XElement(
            ns + "TablixCell",
            new XElement(
                ns + "CellContents",
                new XElement(
                    ns + "Textbox",
                    new XAttribute("Name", $"Textbox_r{rowIndex}_c{columnIndex}"),
                    new XElement(
                        ns + "Paragraphs",
                        new XElement(
                            ns + "Paragraph",
                            new XElement(ns + "TextRuns", textRun))))));
    }
}
