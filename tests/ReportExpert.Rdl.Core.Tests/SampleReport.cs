using System.Reflection;

namespace ReportExpert.Rdl.Core.Tests;

/// <summary>
/// The report definition every test works against.
/// </summary>
/// <remarks>
///  two datasets, two parameters, and a three column tablix with a header row and a detail row.  It is shared with the MCP server test project
/// through <c>tests/Shared/sample-report.rdlc</c>.
/// </remarks>
internal static class SampleReport
{
    /// <summary>The report definition namespace the sample declares.</summary>
    public const string Namespace = "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

    /// <summary>The sample report definition XML.</summary>
    public static string Xml { get; } = LoadEmbedded();

    /// <summary>Parses the sample without touching the file system.</summary>
    /// <returns>The parsed document.</returns>
    public static RdlDocument Parse() => RdlDocument.Parse(Xml);

    private static string LoadEmbedded()
    {
        const string resourceName = "sample-report.rdlc";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The '{resourceName}' test fixture is missing from the assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }
}

/// <summary>
/// A throwaway copy of the sample report on disk, for tests that write.
/// </summary>
internal sealed class TempReport : IDisposable
{
    /// <summary>Creates the temporary report.</summary>
    /// <param name="xml">The content to write, defaulting to the sample report.</param>
    public TempReport(string? xml = null)
    {
        Directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "rdl-mcp-tests",
            Guid.NewGuid().ToString("N"));

        System.IO.Directory.CreateDirectory(Directory);

        Path = System.IO.Path.Combine(Directory, "sample_report.rdl");
        File.WriteAllText(Path, xml ?? SampleReport.Xml);
    }

    /// <summary>Absolute path to the temporary report.</summary>
    public string Path { get; }

    /// <summary>The directory holding the report, removed on dispose.</summary>
    public string Directory { get; }

    /// <summary>Loads the report from disk in its current state.</summary>
    /// <returns>The loaded document.</returns>
    public RdlDocument Load() => RdlDocument.Load(Path);

    /// <summary>Reads the report back as text.</summary>
    /// <returns>The file content.</returns>
    public string ReadText() => File.ReadAllText(Path);

    /// <summary>Removes the temporary directory.</summary>
    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must never fail a test run.
        }
    }
}
