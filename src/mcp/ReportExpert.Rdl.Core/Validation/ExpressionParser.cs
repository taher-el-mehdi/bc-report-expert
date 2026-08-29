using System.Text.RegularExpressions;

namespace ReportExpert.Rdl.Core.Validation;

/// <summary>
/// Extracts dataset field references from RDL expressions.
/// </summary>
/// <remarks>
/// <para>
/// A field reference in RDL does not say which dataset it belongs to. The dataset comes from the
/// enclosing scope, except when a function names one explicitly. Getting this right matters,
/// because otherwise every cross-dataset lookup in a report looks like a broken reference.
/// </para>
/// <para>
/// Three cases are handled, in order of decreasing specificity, and a reference already claimed by
/// an earlier case is never reassigned by a later one:
/// </para>
/// <list type="number">
///   <item><description>
///     <c>Lookup</c>, <c>LookupSet</c> and <c>MultiLookup</c>, where the first argument resolves
///     against the current dataset and the second and third against the dataset named last.
///   </description></item>
///   <item><description>
///     Aggregates such as <c>Sum</c> or <c>First</c>, which may take a dataset name as a scope.
///   </description></item>
///   <item><description>
///     Everything else, which resolves against the current dataset.
///   </description></item>
/// </list>
/// </remarks>
public static partial class ExpressionParser
{
    /// <summary>
    /// Groups the field references in an expression by the dataset each one resolves against.
    /// </summary>
    /// <param name="expression">The expression, including its leading <c>=</c>.</param>
    /// <param name="defaultDataSet">The dataset providing the surrounding scope.</param>
    /// <returns>
    /// A map of dataset name to the distinct field names referenced from it, in first-seen order.
    /// Empty when the text is not an expression.
    /// </returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractWithContext(
        string? expression,
        string defaultDataSet)
    {
        ArgumentNullException.ThrowIfNull(defaultDataSet);

        if (string.IsNullOrWhiteSpace(expression) || !expression.TrimStart().StartsWith('='))
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Positions already attributed to a dataset. Keyed by offset into the expression so that a
        // reference matched by the lookup rule is not re-matched by the broader rules below.
        var claimed = new HashSet<int>();

        void Claim(string dataSet, string field)
        {
            if (!result.TryGetValue(dataSet, out var fields))
            {
                fields = [];
                result[dataSet] = fields;
            }

            if (!fields.Contains(field, StringComparer.Ordinal))
                fields.Add(field);
        }

        void ClaimAllIn(string fragment, int fragmentOffset, string dataSet, bool skipClaimed)
        {
            foreach (Match field in FieldReferenceRegex().Matches(fragment))
            {
                int position = fragmentOffset + field.Index;

                if (skipClaimed && claimed.Contains(position))
                    continue;

                claimed.Add(position);
                Claim(dataSet, field.Groups[1].Value);
            }
        }

        foreach (Match match in LookupRegex().Matches(expression))
        {
            string targetDataSet = match.Groups[5].Value;

            // The source key is evaluated in the current scope; the destination key and the result
            // expression are evaluated inside the dataset being looked up.
            ClaimAllIn(match.Groups[2].Value, match.Groups[2].Index, defaultDataSet, skipClaimed: false);
            ClaimAllIn(match.Groups[3].Value, match.Groups[3].Index, targetDataSet, skipClaimed: false);
            ClaimAllIn(match.Groups[4].Value, match.Groups[4].Index, targetDataSet, skipClaimed: false);
        }

        foreach (Match match in AggregateRegex().Matches(expression))
        {
            var scope = match.Groups[3];
            string targetDataSet = scope.Success ? scope.Value : defaultDataSet;

            ClaimAllIn(match.Groups[2].Value, match.Groups[2].Index, targetDataSet, skipClaimed: true);
        }

        foreach (Match match in FieldReferenceRegex().Matches(expression))
        {
            if (claimed.Contains(match.Index))
                continue;

            Claim(defaultDataSet, match.Groups[1].Value);
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Lists the distinct field names an expression references, ignoring dataset scope.
    /// </summary>
    /// <param name="expression">The expression, including its leading <c>=</c>.</param>
    /// <returns>The field names, in first-seen order. Empty when the text is not an expression.</returns>
    public static IReadOnlyList<string> Extract(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || !expression.TrimStart().StartsWith('='))
            return [];

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in FieldReferenceRegex().Matches(expression))
        {
            string field = match.Groups[1].Value;
            if (seen.Add(field))
                names.Add(field);
        }

        return names;
    }

    /// <summary>
    /// Matches <c>Lookup("key", "destination", "result", "DataSetName")</c> and its set-valued
    /// siblings, capturing each argument separately.
    /// </summary>
    [GeneratedRegex(
        """(Lookup|LookupSet|MultiLookup)\s*\(\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*"([^"]+)"\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex LookupRegex();

    /// <summary>
    /// Matches an aggregate call, capturing its argument and an optional quoted dataset scope.
    /// </summary>
    [GeneratedRegex(
        """(Sum|Count|First|Last|Min|Max|Avg|CountDistinct|StDev|StDevP|Var|VarP|CountRows|RunningValue|Previous)\s*\(\s*([^,)]+?)(?:\s*,\s*"([^"]+)")?\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex AggregateRegex();

    /// <summary>Matches a single <c>Fields!Name</c> reference.</summary>
    [GeneratedRegex(
        @"Fields!(\w+)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex FieldReferenceRegex();
}
