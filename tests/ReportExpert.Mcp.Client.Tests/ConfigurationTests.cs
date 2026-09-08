using ReportExpert.Mcp.Client;

namespace ReportExpert.Mcp.Client.Tests;

/// <summary>
/// Reading the server list, which is hand-editable and therefore frequently malformed.
/// </summary>
public class McpServerConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "rdlc-mcp-config",
        Guid.NewGuid().ToString("N"));

    public McpServerConfigurationTests() => Directory.CreateDirectory(_directory);

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
    public void AMissingFileMeansNoServers()
    {
        Assert.Empty(McpServerConfiguration.Load(Path.Combine(_directory, "absent.json")));
    }

    [Fact]
    public void AMalformedFileMeansNoServers()
    {
        // The application must still start when someone breaks this file by hand.
        string path = Write("{ not json");

        Assert.Empty(McpServerConfiguration.Load(path));
    }

    [Fact]
    public void ServersAreReadInOrder()
    {
        string path = Write("""
            {
              "servers": [
                { "id": "rdl", "command": "rdlc-mcp.exe", "args": ["--verbose"] },
                { "id": "word", "command": "bc-word-layout-mcp.exe" }
              ]
            }
            """);

        var servers = McpServerConfiguration.Load(path);

        Assert.Equal(["rdl", "word"], servers.Select(server => server.Id));
        Assert.Equal(["--verbose"], servers[0].Arguments);
    }

    [Fact]
    public void DisabledServersAreLeftOut()
    {
        string path = Write("""
            {
              "servers": [
                { "id": "rdl", "command": "rdlc-mcp.exe" },
                { "id": "parked", "command": "other.exe", "enabled": false }
              ]
            }
            """);

        Assert.Equal(["rdl"], McpServerConfiguration.Load(path).Select(server => server.Id));
    }

    [Fact]
    public void CommentsAndTrailingCommasAreTolerated()
    {
        // People copy these files between machines and annotate them.
        string path = Write("""
            {
              "servers": [
                // The RDLC server that ships with Report Expert.
                { "id": "rdl", "command": "rdlc-mcp.exe", },
              ],
            }
            """);

        Assert.Single(McpServerConfiguration.Load(path));
    }

    [Fact]
    public void EnvironmentVariablesInPathsAreExpanded()
    {
        string path = Write("""
            { "servers": [ { "id": "rdl", "command": "%SystemRoot%\\rdlc-mcp.exe" } ] }
            """);

        string command = McpServerConfiguration.Load(path)[0].Command;

        Assert.DoesNotContain('%', command);
        Assert.EndsWith("rdlc-mcp.exe", command, StringComparison.Ordinal);
    }

    [Fact]
    public void AppRelativeCommandsAreResolvedAgainstTheApplicationDirectory()
    {
        string path = Write("""
            { "servers": [ { "id": "rdl", "command": "mcp\\rdlc\\rdlc-mcp.exe" } ] }
            """);

        string command = McpServerConfiguration.Load(path)[0].Command;

        Assert.True(Path.IsPathRooted(command));
        Assert.EndsWith(Path.Combine("mcp", "rdlc", "rdlc-mcp.exe"), command, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BareCommandNamesStayOnThePath()
    {
        string path = Write("""
            { "servers": [ { "id": "rdl", "command": "npx" } ] }
            """);

        Assert.Equal("npx", McpServerConfiguration.Load(path)[0].Command);
    }

    [Fact]
    public void WhatIsSavedCanBeLoadedBack()
    {
        string path = Path.Combine(_directory, "roundtrip.json");

        McpServerConfiguration.Save(
        [
            new McpServerDefinition
            {
                Id = "rdl",
                Command = "rdlc-mcp.exe",
                Arguments = ["--quiet"],
                Environment = new Dictionary<string, string> { ["RDLC_MCP_BACKUPS"] = "true" },
            },
        ], path);

        var loaded = McpServerConfiguration.Load(path);

        Assert.Single(loaded);
        Assert.Equal("rdl", loaded[0].Id);
        Assert.Equal(["--quiet"], loaded[0].Arguments);
        Assert.Equal("true", loaded[0].Environment["RDLC_MCP_BACKUPS"]);
    }

    private string Write(string json)
    {
        string path = Path.Combine(_directory, "mcp-servers.json");
        File.WriteAllText(path, json);

        return path;
    }
}

/// <summary>
/// How a qualified tool name is built and taken apart.
/// </summary>
public class QualifiedNameTests
{
    [Fact]
    public void AQualifiedNameIsAcceptableToTheChatCompletionsApi()
    {
        // Function names sent to the API must match ^[A-Za-z0-9_-]{1,64}$, which is why the
        // separator is a double underscore rather than a dot.
        Assert.Matches("^[A-Za-z0-9_-]{1,64}$", McpToolRegistry.Qualify("rdl", "describe_rdl_report"));
    }

    [Theory]
    [InlineData("rdl", "add_column")]
    [InlineData("word", "get_layout")]
    [InlineData("a", "b")]
    public void QualifyAndSplitAreInverses(string serverId, string toolName)
    {
        Assert.True(McpToolRegistry.TrySplit(McpToolRegistry.Qualify(serverId, toolName), out string id, out string tool));

        Assert.Equal(serverId, id);
        Assert.Equal(toolName, tool);
    }

    [Fact]
    public void AToolNameContainingTheSeparatorSurvivesTheRoundTrip()
    {
        Assert.True(McpToolRegistry.TrySplit("rdl__odd__name", out string id, out string tool));

        Assert.Equal("rdl", id);
        Assert.Equal("odd__name", tool);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no_separator_here")]
    [InlineData("__leading")]
    [InlineData("trailing__")]
    public void AMalformedNameIsRejected(string qualifiedName)
    {
        Assert.False(McpToolRegistry.TrySplit(qualifiedName, out _, out _));
    }
}
