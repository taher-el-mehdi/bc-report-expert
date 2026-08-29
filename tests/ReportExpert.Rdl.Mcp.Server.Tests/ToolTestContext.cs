using System.Text.Json;
using ReportExpert.Rdl.Mcp.Server;

namespace ReportExpert.Rdl.Mcp.Server.Tests;

/// <summary>
/// A throwaway report definition on disk, plus helpers for asserting on tool envelopes.
/// </summary>
internal sealed class ToolTestContext : IDisposable
{
    /// <summary>Creates the temporary report.</summary>
    /// <param name="xml">The content to write, defaulting to the sample report.</param>
    /// <param name="fileName">The file name to use.</param>
    public ToolTestContext(string? xml = null, string fileName = "sample_report.rdlc")
    {
        Directory = Path.Combine(Path.GetTempPath(), "rdlc-mcp-tool-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);

        ReportPath = Path.Combine(Directory, fileName);
        File.WriteAllText(ReportPath, xml ?? SampleReport.Xml);
    }

    /// <summary>Absolute path to the temporary report.</summary>
    public string ReportPath { get; }

    /// <summary>The directory holding the report and its backups.</summary>
    public string Directory { get; }

    /// <summary>Reads the report back as text.</summary>
    /// <returns>The file content.</returns>
    public string ReadReport() => File.ReadAllText(ReportPath);

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

/// <summary>
/// Assertions and JSON helpers shared by the tool tests.
/// </summary>
internal static class ToolAssert
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Asserts the response succeeded and returns its payload as JSON for further inspection.
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <returns>The payload.</returns>
    public static JsonElement Ok(ToolResponse response)
    {
        Assert.True(
            response.Ok,
            $"Expected success but got {response.Error?.Code}: {response.Error?.Message}");

        Assert.Null(response.Error);
        Assert.NotNull(response.Data);

        return Serialize(response).GetProperty("data");
    }

    /// <summary>
    /// Asserts the response failed with a specific code and returns the error.
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <param name="expectedCode">The code the failure must carry.</param>
    /// <returns>The error.</returns>
    public static ToolError Failed(ToolResponse response, string expectedCode)
    {
        Assert.False(response.Ok, "Expected a failure but the call succeeded.");
        Assert.NotNull(response.Error);
        Assert.Null(response.Data);
        Assert.Equal(expectedCode, response.Error.Code);

        // A hint is what turns an error into something the caller can act on.
        Assert.False(string.IsNullOrWhiteSpace(response.Error.Hint));

        return response.Error;
    }

    /// <summary>Serializes a response exactly as the MCP transport would.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The serialized envelope.</returns>
    public static JsonElement Serialize(ToolResponse response) =>
        JsonSerializer.SerializeToElement(response, SerializerOptions);

    /// <summary>Reads a string property from a payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    public static string String(JsonElement payload, string name) =>
        payload.GetProperty(name).GetString() ?? string.Empty;
}
