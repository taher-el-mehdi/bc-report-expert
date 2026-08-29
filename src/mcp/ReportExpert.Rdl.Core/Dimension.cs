using System.Globalization;

namespace ReportExpert.Rdl.Core;

/// <summary>
/// Conversion between RDL size strings (<c>"2.5in"</c>, <c>"12pt"</c>, <c>"3cm"</c>) and inches.
/// </summary>
/// <remarks>
/// RDL stores every size as a number followed by a unit suffix. Inches are used as the internal
/// unit because that is what report definitions overwhelmingly use and what width arithmetic
/// (tablix totals, page width) is expressed in.
/// </remarks>
public static class Dimension
{
    /// <summary>
    /// Parses an RDL size string into inches, returning zero for anything unparseable.
    /// </summary>
    /// <param name="value">A size such as <c>"2.5in"</c>. A bare number is read as inches.</param>
    /// <returns>The size in inches, or <c>0</c> if <paramref name="value"/> cannot be parsed.</returns>
    /// <remarks>
    /// Malformed input yields zero rather than throwing. Width arithmetic runs across every column
    /// of a report, and one hand-edited cell should not fail an otherwise valid operation.
    /// Use <see cref="TryToInches"/> when validating caller-supplied input.
    /// </remarks>
    public static double ToInches(string? value) => TryToInches(value, out double inches) ? inches : 0d;

    /// <summary>
    /// Attempts to parse an RDL size string into inches.
    /// </summary>
    /// <param name="value">A size such as <c>"2.5in"</c>. A bare number is read as inches.</param>
    /// <param name="inches">The parsed size, or <c>0</c> when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was understood.</returns>
    public static bool TryToInches(string? value, out double inches)
    {
        inches = 0d;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string text = value.Trim();

        (string Suffix, double PerInch)[] units =
        [
            ("in", 1d),
            ("cm", 2.54d),
            ("mm", 25.4d),
            ("pt", 72d),
            ("pc", 6d),
        ];

        foreach ((string suffix, double perInch) in units)
        {
            if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            string number = text[..^suffix.Length].Trim();
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return false;

            inches = parsed / perInch;
            return true;
        }

        // No recognised suffix: RDL treats a bare number as inches.
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bare))
            return false;

        inches = bare;
        return true;
    }

    /// <summary>
    /// Formats a measurement in inches as an RDL size string with two decimal places.
    /// </summary>
    /// <param name="inches">The measurement in inches.</param>
    /// <returns>A string such as <c>"3.50in"</c>.</returns>
    public static string FromInches(double inches) =>
        string.Create(CultureInfo.InvariantCulture, $"{inches:F2}in");

    /// <summary>
    /// Validates that a caller-supplied size string is well formed.
    /// </summary>
    /// <param name="value">The size string to check.</param>
    /// <param name="parameterName">Name of the argument being validated, used in the error message.</param>
    /// <exception cref="ArgumentException">The value is missing or not a valid RDL size.</exception>
    public static void Validate(string? value, string parameterName)
    {
        if (!TryToInches(value, out double inches) || inches < 0)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid size. Use a number followed by in, cm, mm, pt or pc, for example \"2.5in\".",
                parameterName);
        }
    }
}
