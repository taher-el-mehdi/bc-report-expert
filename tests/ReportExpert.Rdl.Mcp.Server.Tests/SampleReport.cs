using System.Reflection;

namespace ReportExpert.Rdl.Mcp.Server.Tests;

/// <summary>
/// The report definition the tool tests work against.
/// </summary>
/// <remarks>
/// Shared with the domain test project through <c>tests/Shared/sample-report.rdlc</c>, so that
/// tool-level and domain-level assertions describe the same report.
/// </remarks>
internal static class SampleReport
{
    /// <summary>The report definition namespace the sample declares.</summary>
    public const string Namespace = "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

    /// <summary>The sample report definition XML.</summary>
    public static string Xml { get; } = LoadEmbedded();

    private static string LoadEmbedded()
    {
        const string resourceName = "sample-report.rdlc";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The '{resourceName}' test fixture is missing from the assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }
}
