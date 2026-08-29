using System.Reflection;
using ReportExpert.Mcp.Client;

namespace ReportExpert.Mcp.Client.Tests;

/// <summary>
/// Locates the RDLC MCP server executable that was built alongside these tests.
/// </summary>
/// <remarks>
/// The server is referenced with <c>ReferenceOutputAssembly=false</c>, so it is built but not
/// linked. That keeps the client free of any compile-time knowledge of a specific server while
/// still guaranteeing there is a real one to launch.
/// </remarks>
internal static class ServerExecutable
{
    /// <summary>The absolute path to the built server.</summary>
    /// <exception cref="InvalidOperationException">The server has not been built.</exception>
    public static string Path { get; } = Locate();

    /// <summary>A server definition pointing at it.</summary>
    /// <param name="id">The identifier to namespace its tools under.</param>
    /// <returns>The definition.</returns>
    public static McpServerDefinition Definition(string id = "rdl") => new()
    {
        Id = id,
        Command = Path,
    };

    private static string Locate()
    {
        string testBinary = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // tests/<project>/bin/<config>/<tfm> -> repo root
        var directory = new DirectoryInfo(testBinary);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src", "mcp")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not find the repository root from the test output directory.");

        string configuration = testBinary.Contains($"{System.IO.Path.DirectorySeparatorChar}Release{System.IO.Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        string executable = System.IO.Path.Combine(
            directory.FullName,
            "src", "mcp", "ReportExpert.Rdl.Mcp.Server", "bin", configuration,
            System.IO.Path.GetFileName(testBinary),
            OperatingSystem.IsWindows() ? "rdlc-mcp.exe" : "rdlc-mcp");

        if (!File.Exists(executable))
        {
            throw new InvalidOperationException(
                $"The RDLC MCP server was not found at '{executable}'. Build the solution before running these tests.");
        }

        return executable;
    }
}

/// <summary>
/// A throwaway report definition for the end-to-end tests to operate on.
/// </summary>
internal sealed class TempReport : IDisposable
{
    /// <summary>Creates the temporary report.</summary>
    public TempReport()
    {
        Directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "rdlc-mcp-e2e",
            Guid.NewGuid().ToString("N"));

        System.IO.Directory.CreateDirectory(Directory);

        Path = System.IO.Path.Combine(Directory, "sample_report.rdlc");
        File.WriteAllText(Path, ReadFixture());
    }

    /// <summary>Absolute path to the report.</summary>
    public string Path { get; }

    /// <summary>The directory holding the report, removed on dispose.</summary>
    public string Directory { get; }

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

    private static string ReadFixture()
    {
        const string resourceName = "sample-report.rdlc";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The '{resourceName}' test fixture is missing from the assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
