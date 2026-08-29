using System.Text.Json;
using ReportExpert.Mcp.Client;

namespace ReportExpert.Mcp.Client.Tests;

public sealed class FilteringMcpToolGatewayTests
{
    [Fact]
    public async Task ListToolsAsync_OmitsDisabledTools()
    {
        var inner = new FakeGateway(
        [
            Descriptor("rdl", "add_column"),
            Descriptor("rdl", "get_columns"),
        ]);

        var gateway = new FilteringMcpToolGateway(
            inner,
            name => !string.Equals(name, "rdl__add_column", StringComparison.Ordinal));

        var tools = await gateway.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("get_columns", tools[0].ToolName);
    }

    [Fact]
    public async Task ListAllToolsAsync_IncludesDisabledTools()
    {
        var inner = new FakeGateway([Descriptor("rdl", "add_column")]);
        var gateway = new FilteringMcpToolGateway(inner, _ => false);

        var all = await gateway.ListAllToolsAsync();

        Assert.Single(all);
        Assert.False(gateway.IsToolEnabled("rdl__add_column"));
    }

    [Fact]
    public async Task CallToolAsync_BlocksDisabledTool()
    {
        var inner = new FakeGateway([Descriptor("rdl", "add_column")]);
        var gateway = new FilteringMcpToolGateway(inner, _ => false);

        var result = await gateway.CallToolAsync("rdl__add_column", null);

        Assert.True(result.IsError);
        Assert.Contains("enable the tool 'add_column'", result.Text, StringComparison.Ordinal);
        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task CallToolAsync_RunsEnabledTool()
    {
        var inner = new FakeGateway([Descriptor("rdl", "get_columns")]);
        var gateway = new FilteringMcpToolGateway(inner, _ => true);

        var result = await gateway.CallToolAsync("rdl__get_columns", null);

        Assert.False(result.IsError);
        Assert.Equal(1, inner.CallCount);
    }

    private static McpToolDescriptor Descriptor(string server, string tool) =>
        new(server, tool, $"{server}__{tool}", $"{tool} description", default(JsonElement));

    private sealed class FakeGateway(IReadOnlyList<McpToolDescriptor> tools) : IMcpToolGateway
    {
        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(tools);

        public ValueTask<McpToolResult> CallToolAsync(
            string qualifiedName,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(new McpToolResult(false, "ok"));
        }
    }
}
