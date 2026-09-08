using System.Text.Json;
using ReportExpert.Mcp.Client;

namespace ReportExpert.Mcp.Client.Tests;

/// <summary>
/// The gateway driving the real server executable over stdio.
/// </summary>
/// <remarks>
/// These are deliberately not mocked. Everything that can go wrong between the application and
/// the server — process launch, argument marshalling, the shape of the result — only goes wrong
/// against a real process.
/// </remarks>
[Collection(nameof(ServerCollection))]
public class EndToEndTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ListToolsAsync_ReturnsTheServersToolsWithQualifiedNames()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var tools = await registry.ListToolsAsync(cancellation.Token);

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.Equal("rdl", tool.ServerId));
        Assert.Contains(tools, tool => tool.QualifiedName == "rdl__describe_rdl_report");
    }

    [Fact]
    public async Task ListToolsAsync_CarriesTheDescriptionAndSchemaTheModelNeeds()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var tools = await registry.ListToolsAsync(cancellation.Token);
        var describe = tools.Single(tool => tool.ToolName == "describe_rdl_report");

        Assert.False(string.IsNullOrWhiteSpace(describe.Description));

        var properties = describe.JsonSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("filepath", out _));
    }

    [Fact]
    public async Task ListToolsAsync_PreservesTheReadOnlyHint()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var tools = await registry.ListToolsAsync(cancellation.Token);

        // This hint is what the approval gate reads to decide whether to prompt the user.
        Assert.True(tools.Single(tool => tool.ToolName == "get_rdl_columns").IsReadOnly);
        Assert.False(tools.Single(tool => tool.ToolName == "add_column").IsReadOnly);
    }

    [Fact]
    public async Task CallToolAsync_RoundTripsARead()
    {
        using var report = new TempReport();
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync(
            "rdl__get_rdl_columns",
            new Dictionary<string, object?> { ["filepath"] = report.Path },
            cancellation.Token);

        Assert.False(result.IsError);

        using var payload = JsonDocument.Parse(result.Text);
        Assert.True(payload.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(3, payload.RootElement.GetProperty("data").GetProperty("columns").GetArrayLength());
    }

    [Fact]
    public async Task CallToolAsync_RoundTripsAWrite()
    {
        using var report = new TempReport();
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync(
            "rdl__update_column_header",
            new Dictionary<string, object?>
            {
                ["filepath"] = report.Path,
                ["old_header"] = "Name",
                ["new_header"] = "Customer Name",
            },
            cancellation.Token);

        Assert.False(result.IsError);
        Assert.Contains("Customer Name", report.ReadText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallToolAsync_HonoursDryRunAcrossTheWire()
    {
        using var report = new TempReport();
        string before = report.ReadText();

        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync(
            "rdl__update_column_header",
            new Dictionary<string, object?>
            {
                ["filepath"] = report.Path,
                ["old_header"] = "Name",
                ["new_header"] = "Customer Name",
                ["dry_run"] = true,
            },
            cancellation.Token);

        Assert.False(result.IsError);
        Assert.Contains("diff", result.Text, StringComparison.Ordinal);
        Assert.Equal(before, report.ReadText());
    }

    [Fact]
    public async Task CallToolAsync_SurfacesAToolFailureAsAReadableResult()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync(
            "rdl__describe_rdl_report",
            new Dictionary<string, object?> { ["filepath"] = "C:\\nowhere\\missing.rdlc" },
            cancellation.Token);

        // The server reports a failed operation inside the envelope, not as a protocol error, so
        // the model can read the hint and correct itself.
        using var payload = JsonDocument.Parse(result.Text);
        Assert.False(payload.RootElement.GetProperty("ok").GetBoolean());

        var error = payload.RootElement.GetProperty("error");
        Assert.Equal("file_not_found", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("hint").GetString()));
    }

    [Fact]
    public async Task CallToolAsync_ReportsABadArgumentRatherThanThrowing()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync(
            "rdl__describe_rdl_report",
            new Dictionary<string, object?> { ["wrong_argument"] = 42 },
            cancellation.Token);

        Assert.True(result.IsError);
        Assert.False(string.IsNullOrWhiteSpace(result.Text));
    }

    [Fact]
    public async Task CallToolAsync_ReportsAnUnknownServerWithoutLaunchingAnything()
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var result = await registry.CallToolAsync("other__do_something", arguments: null, cancellation.Token);

        Assert.True(result.IsError);
        Assert.Contains("other", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServerUnderAPathWithSpacesCanStillStart()
    {
        // Store, Program Files, and this repository all have spaces in the path. The MCP SDK's
        // default cmd.exe /c wrap splits on the first space unless we quote it ourselves.
        string staged = Path.Combine(Path.GetTempPath(), "Report Expert mcp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staged);

        try
        {
            CopyDirectory(Path.GetDirectoryName(ServerExecutable.Path)!, staged);

            string exe = Path.Combine(staged, Path.GetFileName(ServerExecutable.Path));
            using var cancellation = new CancellationTokenSource(Timeout);
            await using var registry = new McpToolRegistry(
            [
                new McpServerDefinition { Id = "rdl", Command = exe },
            ]);

            var tools = await registry.ListToolsAsync(cancellation.Token);

            Assert.Contains(tools, tool => tool.QualifiedName == "rdl__describe_rdl_report");
        }
        finally
        {
            try
            {
                if (Directory.Exists(staged))
                    Directory.Delete(staged, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory must never fail a test run.
            }
        }
    }

    [Fact]
    public async Task AServerThatCannotStartIsSkippedRatherThanFailingTheCatalogue()
    {
        using var cancellation = new CancellationTokenSource(Timeout);

        await using var registry = new McpToolRegistry(
        [
            new McpServerDefinition { Id = "broken", Command = "this-executable-does-not-exist" },
            ServerExecutable.Definition(),
        ]);

        var tools = await registry.ListToolsAsync(cancellation.Token);

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.Equal("rdl", tool.ServerId));
    }

    [Fact]
    public async Task TheSameConnectionIsReusedAcrossCalls()
    {
        using var report = new TempReport();
        using var cancellation = new CancellationTokenSource(Timeout);
        await using var registry = new McpToolRegistry([ServerExecutable.Definition()]);

        var arguments = new Dictionary<string, object?> { ["filepath"] = report.Path };

        for (int call = 0; call < 3; call++)
        {
            var result = await registry.CallToolAsync("rdl__validate_rdl", arguments, cancellation.Token);
            Assert.False(result.IsError);
        }

        // Three calls, one child process: the connection is established lazily and then kept.
        Assert.True(registry.Connections.Single().IsConnected);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (string child in Directory.GetDirectories(source))
            CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
    }
}

/// <summary>
/// Keeps the end-to-end tests from launching a swarm of child processes at once.
/// </summary>
[CollectionDefinition(nameof(ServerCollection), DisableParallelization = true)]
public class ServerCollection;
