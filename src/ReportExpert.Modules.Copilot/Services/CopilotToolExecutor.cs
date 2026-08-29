using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReportExpert.Modules.Copilot.Services;

public sealed record CopilotToolCall(string Name, string ArgumentsJson);

/// <summary>Runs validate/preview/fix tools invoked by the agent.</summary>
public sealed partial class CopilotToolExecutor
{
  public Func<Task<string>>? GetCurrentXmlAsync { get; set; }
  public Func<Task<string>>? RunPreviewAsync { get; set; }
  public Func<string, string, Task<string>>? ApplyPatchAsync { get; set; }

  public static bool TryParseToolCall(string response, out CopilotToolCall? toolCall)
  {
    toolCall = null;
    var match = ToolCallRegex().Match(response);
    if (!match.Success)
      return false;

    try
    {
      using var doc = JsonDocument.Parse(match.Groups[1].Value);
      string name = doc.RootElement.GetProperty("name").GetString() ?? string.Empty;
      string args = doc.RootElement.TryGetProperty("arguments", out var argsEl)
        ? argsEl.GetRawText()
        : "{}";
      toolCall = new CopilotToolCall(name, args);
      return name.Length > 0;
    }
    catch (Exception)
    {
      // Any malformed payload is treated as "not a tool call" so chat can continue.
      return false;
    }
  }

  public async Task<string> ExecuteAsync(CopilotToolCall toolCall)
  {
    return toolCall.Name switch
    {
      "validate_xml" => await ValidateXmlAsync(),
      "run_preview" => await RunPreviewToolAsync(),
      "suggest_fix" => await SuggestFixAsync(toolCall.ArgumentsJson),
      _ => $"Unknown tool: {toolCall.Name}"
    };
  }

  private async Task<string> ValidateXmlAsync()
  {
    string xml = GetCurrentXmlAsync is null ? string.Empty : await GetCurrentXmlAsync();
    var (ok, message) = RdlcXmlValidator.Validate(xml);
    return ok ? $"VALID: {message}" : $"INVALID: {message}";
  }

  private async Task<string> RunPreviewToolAsync()
  {
    if (RunPreviewAsync is null)
      return "Preview is not available from the shell.";

    return await RunPreviewAsync();
  }

  private async Task<string> SuggestFixAsync(string argumentsJson)
  {
    string issue = "validation errors";
    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      if (doc.RootElement.TryGetProperty("issue", out var issueEl))
        issue = issueEl.GetString() ?? issue;
    }
    catch (JsonException)
    {
      // Optional "issue" argument; keep the default label if JSON is malformed.
    }

    string xml = GetCurrentXmlAsync is null ? string.Empty : await GetCurrentXmlAsync();
    var (ok, message) = RdlcXmlValidator.Validate(xml);
    if (ok)
      return $"No validation issues found for: {issue}";

    return $"Issue: {issue}\nValidation: {message}\nProvide an rdlc-patch block with find/replace to fix this.";
  }

  [GeneratedRegex(@"```copilot-tool\s*\r?\n([\s\S]*?)```", RegexOptions.IgnoreCase)]
  private static partial Regex ToolCallRegex();
}
