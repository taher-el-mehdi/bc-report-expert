using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Reading;

/// <summary>
/// Read-only access to the columns of a tablix.
/// </summary>
public static class ColumnReader
{
    /// <summary>
    /// Reads the columns of a tablix, pairing each column definition with its header cell and its
    /// detail cell.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="tablixName">
    /// The tablix to read, or <see langword="null"/> for the first tablix in the report.
    /// </param>
    /// <returns>
    /// The columns. When the report contains no tablix at all the list is empty and
    /// <see cref="ColumnsResult.Error"/> explains why.
    /// </returns>
    public static ColumnsResult GetColumns(RdlDocument document, string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        if (string.IsNullOrWhiteSpace(tablixName) && TablixNavigator.AllTablixes(document).Count == 0)
            return new ColumnsResult { Columns = [], Error = "No Tablix found" };

        var tablix = TablixNavigator.RequireTablix(document, tablixName);

        var widths = TablixNavigator
            .Columns(tablix, ns)
            .Select(column => column.Element(ns + "Width")?.Value ?? string.Empty)
            .ToList();

        var headerRow = TablixNavigator.FindRow(tablix, ns, TablixRowKind.Header);
        var dataRow = TablixNavigator.FindRow(tablix, ns, TablixRowKind.Data);

        var columns = new List<ColumnInfo>();

        if (headerRow is not null)
        {
            var headerCells = TablixNavigator.Cells(headerRow, ns);

            for (int index = 0; index < headerCells.Count; index++)
            {
                var textbox = TablixNavigator.FirstTextbox(headerCells[index], ns);
                string headerText = string.Empty;
                string textboxName = string.Empty;

                if (textbox is not null)
                {
                    textboxName = textbox.Attribute("Name")?.Value ?? string.Empty;
                    string? value = textbox.Descendants(ns + "Value").FirstOrDefault()?.Value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        // A header built from an expression is shown as the field it displays,
                        // which reads far better than the raw expression in a column listing.
                        headerText = value.StartsWith('=') && TryParseFieldName(value, out string? field)
                            ? field
                            : value;
                    }
                }

                columns.Add(new ColumnInfo
                {
                    Index = index,
                    Header = headerText,
                    Width = index < widths.Count ? widths[index] : string.Empty,
                    TextboxName = textboxName,
                });
            }
        }

        if (dataRow is not null)
        {
            var dataCells = TablixNavigator.Cells(dataRow, ns);

            for (int index = 0; index < dataCells.Count && index < columns.Count; index++)
            {
                var textbox = TablixNavigator.FirstTextbox(dataCells[index], ns);
                if (textbox is null)
                    continue;

                string? binding = textbox.Descendants(ns + "Value").FirstOrDefault()?.Value;
                string? format = textbox.Descendants(ns + "Format").FirstOrDefault()?.Value;

                columns[index] = columns[index] with
                {
                    FieldBinding = string.IsNullOrEmpty(binding) ? null : binding,
                    FieldName = !string.IsNullOrEmpty(binding) && binding.StartsWith('=')
                        && TryParseFieldName(binding, out string? field) ? field : null,
                    Format = string.IsNullOrEmpty(format) ? null : format,
                };
            }
        }

        return new ColumnsResult { Columns = columns };
    }

    /// <summary>
    /// Extracts the field name from the first <c>Fields!Name</c> reference in an expression.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="fieldName">The field name when one was found.</param>
    /// <returns><see langword="true"/> when the expression references a field.</returns>
    private static bool TryParseFieldName(string expression, out string fieldName)
    {
        fieldName = string.Empty;

        const string marker = "Fields!";
        int start = expression.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += marker.Length;

        int end = start;
        while (end < expression.Length && (char.IsLetterOrDigit(expression[end]) || expression[end] == '_'))
            end++;

        if (end == start)
            return false;

        fieldName = expression[start..end];
        return true;
    }
}
