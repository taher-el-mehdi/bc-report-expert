using System.Text;

namespace ReportExpert.Rdl.Mcp.Server;

/// <summary>
/// Produces a unified diff between two versions of a text document.
/// </summary>
/// <remarks>
/// Used to show what a mutating tool would do when it is called with <c>dry_run</c>. Report
/// definitions are large and edits are small, so identical leading and trailing lines are trimmed
/// before the expensive comparison runs.
/// </remarks>
internal static class UnifiedDiff
{
    private const int ContextLines = 3;

    /// <summary>
    /// Guards the quadratic comparison. Beyond this many changed lines the diff is summarized
    /// instead, which is better than making a dry run slower than the real edit.
    /// </summary>
    private const int MaxChangedLines = 4000;

    /// <summary>
    /// Builds a unified diff.
    /// </summary>
    /// <param name="before">The original text.</param>
    /// <param name="after">The modified text.</param>
    /// <param name="label">A name for the file, used in the diff header.</param>
    /// <returns>The diff, or a note when the two texts are identical.</returns>
    public static string Create(string before, string after, string label)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
            return "(no changes)";

        string[] left = SplitLines(before);
        string[] right = SplitLines(after);

        int prefix = CommonPrefix(left, right);
        int suffix = CommonSuffix(left, right, prefix);

        int leftCount = left.Length - prefix - suffix;
        int rightCount = right.Length - prefix - suffix;

        if (leftCount > MaxChangedLines || rightCount > MaxChangedLines)
        {
            return $"--- a/{label}\n+++ b/{label}\n" +
                   $"(the change spans {leftCount} removed and {rightCount} added lines, too large to display)";
        }

        var builder = new StringBuilder();
        builder.Append("--- a/").Append(label).Append('\n');
        builder.Append("+++ b/").Append(label).Append('\n');

        int contextStart = Math.Max(0, prefix - ContextLines);
        int leftContextEnd = Math.Min(left.Length, prefix + leftCount + ContextLines);
        int rightContextEnd = Math.Min(right.Length, prefix + rightCount + ContextLines);

        builder
            .Append("@@ -")
            .Append(contextStart + 1).Append(',').Append(leftContextEnd - contextStart)
            .Append(" +")
            .Append(contextStart + 1).Append(',').Append(rightContextEnd - contextStart)
            .Append(" @@\n");

        for (int i = contextStart; i < prefix; i++)
            builder.Append(' ').Append(left[i]).Append('\n');

        foreach (var (kind, text) in Compare(
                     left.AsSpan(prefix, leftCount).ToArray(),
                     right.AsSpan(prefix, rightCount).ToArray()))
        {
            builder.Append(kind).Append(text).Append('\n');
        }

        for (int i = prefix + leftCount; i < leftContextEnd; i++)
            builder.Append(' ').Append(left[i]).Append('\n');

        return builder.ToString();
    }

    /// <summary>
    /// Longest-common-subsequence comparison of the two changed regions.
    /// </summary>
    private static List<(char Kind, string Text)> Compare(string[] left, string[] right)
    {
        int[,] lengths = new int[left.Length + 1, right.Length + 1];

        for (int i = left.Length - 1; i >= 0; i--)
        {
            for (int j = right.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = string.Equals(left[i], right[j], StringComparison.Ordinal)
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var result = new List<(char, string)>();
        int x = 0;
        int y = 0;

        while (x < left.Length && y < right.Length)
        {
            if (string.Equals(left[x], right[y], StringComparison.Ordinal))
            {
                result.Add((' ', left[x]));
                x++;
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                result.Add(('-', left[x]));
                x++;
            }
            else
            {
                result.Add(('+', right[y]));
                y++;
            }
        }

        for (; x < left.Length; x++)
            result.Add(('-', left[x]));

        for (; y < right.Length; y++)
            result.Add(('+', right[y]));

        return result;
    }

    private static string[] SplitLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static int CommonPrefix(string[] left, string[] right)
    {
        int limit = Math.Min(left.Length, right.Length);
        int count = 0;

        while (count < limit && string.Equals(left[count], right[count], StringComparison.Ordinal))
            count++;

        return count;
    }

    private static int CommonSuffix(string[] left, string[] right, int prefix)
    {
        int limit = Math.Min(left.Length, right.Length) - prefix;
        int count = 0;

        while (count < limit &&
               string.Equals(left[^(count + 1)], right[^(count + 1)], StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
