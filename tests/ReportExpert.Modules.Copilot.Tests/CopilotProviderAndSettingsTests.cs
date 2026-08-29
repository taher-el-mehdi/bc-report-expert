using ReportExpert.Modules.Copilot.Models;
using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Copilot.Tests;

public class CopilotProviderDefaultsTests
{
    [Fact]
    public void AllProviders_HaveDefaults()
    {
        foreach (CopilotProvider provider in CopilotProviderDefaults.AllProviders)
        {
            Assert.False(string.IsNullOrWhiteSpace(CopilotProviderDefaults.DefaultBaseUrl(provider)));
            Assert.False(string.IsNullOrWhiteSpace(CopilotProviderDefaults.DefaultModel(provider)));
            Assert.False(string.IsNullOrWhiteSpace(CopilotProviderDefaults.DisplayName(provider)));
            Assert.False(string.IsNullOrWhiteSpace(CopilotProviderDefaults.ApiKeyUrl(provider)));
            Assert.NotEmpty(CopilotProviderDefaults.Models(provider));
        }
    }

    [Fact]
    public void Ollama_UsesLocalhost()
    {
        Assert.Contains("localhost", CopilotProviderDefaults.DefaultBaseUrl(CopilotProvider.Ollama), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Azure_BaseUrl_IsAPlaceholderNotASecret()
    {
        string url = CopilotProviderDefaults.DefaultBaseUrl(CopilotProvider.AzureOpenAI);
        Assert.Contains("YOUR-RESOURCE", url, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", url, StringComparison.Ordinal);
    }
}

public class CopilotSettingsTests
{
    [Fact]
    public void IsMcpToolEnabled_DefaultsToTrue()
    {
        var settings = new CopilotSettings();
        Assert.True(settings.IsMcpToolEnabled("rdl__describe_rdl_report"));
    }

    [Fact]
    public void SetMcpToolEnabled_DisableThenEnable_RoundTrips()
    {
        var settings = new CopilotSettings();
        const string name = "rdl__set_column_hidden";

        Assert.True(settings.SetMcpToolEnabled(name, enabled: false));
        Assert.False(settings.IsMcpToolEnabled(name));
        Assert.False(settings.SetMcpToolEnabled(name, enabled: false));

        Assert.True(settings.SetMcpToolEnabled(name, enabled: true));
        Assert.True(settings.IsMcpToolEnabled(name));
        Assert.Empty(settings.DisabledMcpTools);
    }

    [Fact]
    public void SetMcpToolEnabled_BlankName_DoesNotChange()
    {
        var settings = new CopilotSettings();
        Assert.False(settings.SetMcpToolEnabled("  ", enabled: false));
        Assert.Empty(settings.DisabledMcpTools);
    }
}

public class CopilotToolExecutorTests
{
    [Fact]
    public void TryParseToolCall_ReadsNameAndArguments()
    {
        const string response = """
            ```copilot-tool
            {"name":"validate_xml","arguments":{"issue":"missing body"}}
            ```
            """;

        Assert.True(CopilotToolExecutor.TryParseToolCall(response, out CopilotToolCall? call));
        Assert.NotNull(call);
        Assert.Equal("validate_xml", call.Name);
        Assert.Contains("missing body", call.ArgumentsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseToolCall_NonToolText_ReturnsFalse()
    {
        Assert.False(CopilotToolExecutor.TryParseToolCall("just a chat reply", out CopilotToolCall? call));
        Assert.Null(call);
    }
}
