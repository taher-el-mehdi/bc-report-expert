using ReportExpert.Mcp.Client;

namespace ReportExpert.Mcp.Client.Tests;

/// <summary>
/// How the shipped server path is found and how leftover machine-specific config is repaired.
/// </summary>
public class BundledMcpLocatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "rdlc-mcp-locator",
        Guid.NewGuid().ToString("N"));

    public BundledMcpLocatorTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must never fail a test run.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FindReturnsTheNestedServerWhenItsDependenciesArePresent()
    {
        string exe = WriteRunnableServer(Path.Combine(_directory, "mcp", "rdlc"));

        Assert.Equal(exe, BundledMcpLocator.Find(_directory));
    }

    [Fact]
    public void FindIgnoresALoneExeWithoutDependencies()
    {
        string folder = Path.Combine(_directory, "mcp", "rdlc");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, BundledMcpLocator.ExecutableFileName), [0]);

        Assert.Null(BundledMcpLocator.Find(_directory));
    }

    [Fact]
    public void ResolveCommandKeepsAbsolutePaths()
    {
        string absolute = Path.Combine(_directory, "rdlc-mcp.exe");

        Assert.Equal(Path.GetFullPath(absolute), BundledMcpLocator.ResolveCommand(absolute, _directory));
    }

    [Fact]
    public void ResolveCommandMapsThePortableRelativeCommand()
    {
        string resolved = BundledMcpLocator.ResolveCommand(BundledMcpLocator.RelativeCommand, _directory);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(_directory, "mcp", "rdlc", BundledMcpLocator.ExecutableFileName)),
            resolved);
    }

    [Fact]
    public void RepairReplacesAHardcodedDebugPathWithThePortableCommand()
    {
        var servers = new[]
        {
            new McpServerDefinition
            {
                Id = "rdl",
                Command = @"C:\Users\hp\Documents\Personal-projects\Report Expert\src\ReportExpert.App\bin\Debug\net10.0-windows\mcp\rdlc\rdlc-mcp.exe",
            },
            new McpServerDefinition { Id = "word", Command = "other.exe" },
        };

        var repaired = BundledMcpLocator.Repair(servers, bundledPath: "ignored");

        Assert.Equal(BundledMcpLocator.RelativeCommand, repaired[0].Command);
        Assert.Equal("other.exe", repaired[1].Command);
    }

    [Fact]
    public void RepairLeavesAnAlreadyPortableRdlEntryAlone()
    {
        var servers = new[]
        {
            new McpServerDefinition { Id = "rdl", Command = BundledMcpLocator.RelativeCommand },
        };

        Assert.Same(servers, BundledMcpLocator.Repair(servers, bundledPath: "ignored"));
    }

    [Fact]
    public void RepairAddsTheBundledServerWhenItIsMissing()
    {
        var servers = new[]
        {
            new McpServerDefinition { Id = "word", Command = "other.exe" },
        };

        var repaired = BundledMcpLocator.Repair(servers, bundledPath: Path.Combine(_directory, "rdlc-mcp.exe"));

        Assert.Equal(2, repaired.Count);
        Assert.Equal("rdl", repaired[1].Id);
        Assert.Equal(BundledMcpLocator.RelativeCommand, repaired[1].Command);
    }

    private static string WriteRunnableServer(string directory)
    {
        Directory.CreateDirectory(directory);
        string exe = Path.Combine(directory, BundledMcpLocator.ExecutableFileName);
        File.WriteAllBytes(exe, [0]);
        File.WriteAllBytes(Path.Combine(directory, "Microsoft.Extensions.Hosting.dll"), [0]);
        File.WriteAllBytes(Path.Combine(directory, "ReportExpert.Rdl.Core.dll"), [0]);
        return exe;
    }
}

/// <summary>
/// Windows cmd.exe launch used so Store and Program Files paths can start.
/// </summary>
public class McpStdioLaunchTests
{
    [Fact]
    public void ApplyUsesCmdOnWindowsWithThePathAsItsOwnArgument()
    {
        string exe = Path.Combine(Path.GetTempPath(), "Report Expert", "rdlc-mcp.exe");
        var options = new ModelContextProtocol.Client.StdioClientTransportOptions
        {
            Command = "placeholder",
        };

        McpStdioLaunch.Apply(options, new McpServerDefinition { Id = "rdl", Command = exe });

        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal("cmd.exe", options.Command);
        Assert.Equal(["/c", Path.GetFullPath(exe)], options.Arguments);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(exe)), options.WorkingDirectory);
    }

    [Fact]
    public void IsDirectExecutableIsTrueForAnExistingExe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "rdlc-mcp-launch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string exe = Path.Combine(directory, "rdlc-mcp.exe");

        try
        {
            File.WriteAllBytes(exe, [0]);
            Assert.True(McpStdioLaunch.IsDirectExecutable(exe));
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }

    [Fact]
    public void IsDirectExecutableIsFalseForAShellCommand()
    {
        Assert.False(McpStdioLaunch.IsDirectExecutable("npx"));
    }
}
