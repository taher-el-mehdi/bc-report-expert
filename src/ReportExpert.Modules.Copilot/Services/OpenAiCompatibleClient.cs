using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportExpert.Modules.Copilot.Models;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// Unified HTTP client for Groq, Gemini, Mistral, OpenRouter, Claude, OpenAI, Azure, and Ollama
/// (all expose an OpenAI-compatible <c>/chat/completions</c> endpoint).
/// </summary>
public sealed class OpenAiCompatibleClient : IChatCompletionClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly CopilotSettingsService _settingsService;

    public OpenAiCompatibleClient(CopilotSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var completion = await CompleteAsync(new ChatCompletionRequest(messages), cancellationToken);
        return completion.Content;
    }

    public async Task<ChatCompletion> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _settingsService.Settings;
        if (settings.Provider != CopilotProvider.Ollama && string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("No API key configured. Open Copilot settings and enter your key.");

        string url = BuildCompletionsUrl(settings);

        var payload = new JsonObject
        {
            ["model"] = settings.Model,
            ["messages"] = BuildMessages(request.Messages),
            ["temperature"] = 0.2
        };

        if (request.Tools.Count > 0)
        {
            payload["tools"] = BuildTools(request.Tools);
            payload["tool_choice"] = request.ToolChoice;

            // Groq rejects the whole completion when the model names a tool that is not in
            // request.tools. Prefer recovering in the agent loop over failing the turn.
            if (settings.Provider == CopilotProvider.Groq)
                payload["disable_tool_validation"] = true;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        if (settings.Provider == CopilotProvider.AzureOpenAI)
            message.Headers.Add("api-key", settings.ApiKey);

        // OpenRouter ranks apps that identify themselves; optional but improves rate limits.
        if (settings.Provider == CopilotProvider.OpenRouter)
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/taher-el-mehdi/bc-report-expert");
            message.Headers.TryAddWithoutValidation("X-Title", "Report Expert");
        }

        message.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(message, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string detail = TryExtractError(body) ?? body;
            throw new HttpRequestException($"{CopilotProviderDefaults.DisplayName(settings.Provider)} API error ({(int)response.StatusCode}): {detail}");
        }

        return ParseCompletion(body);
    }

    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var array = new JsonArray();

        foreach (var message in messages)
        {
            var node = new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };

            if (message.ToolCallId is not null)
                node["tool_call_id"] = message.ToolCallId;

            if (message.Name is not null)
                node["name"] = message.Name;

            if (message.ToolCalls is { Count: > 0 } calls)
            {
                var callArray = new JsonArray();

                foreach (var call in calls)
                {
                    callArray.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = call.ArgumentsJson
                        }
                    });
                }

                node["tool_calls"] = callArray;
            }

            array.Add(node);
        }

        return array;
    }

    private static JsonArray BuildTools(IReadOnlyList<ChatTool> tools)
    {
        var array = new JsonArray();

        foreach (var tool in tools)
        {
            array.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.ParametersSchema.GetRawText())
                }
            });
        }

        return array;
    }

    private static ChatCompletion ParseCompletion(string body)
    {
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return new ChatCompletion(string.Empty, []);

        var message = choices[0].GetProperty("message");

        string content = message.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString() ?? string.Empty
            : string.Empty;

        if (!message.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.ValueKind != JsonValueKind.Array)
            return new ChatCompletion(content, []);

        var calls = new List<ChatToolCall>(toolCalls.GetArrayLength());

        foreach (var call in toolCalls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out var function))
                continue;

            string name = function.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            if (name.Length == 0)
                continue;

            // Arguments arrive as a JSON string rather than an object, and an empty one is
            // normal for a tool whose parameters are all optional.
            string arguments = function.TryGetProperty("arguments", out var argumentsElement)
                ? argumentsElement.GetString() ?? "{}"
                : "{}";

            string id = call.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? string.Empty
                : string.Empty;

            calls.Add(new ChatToolCall(id, name, arguments.Length == 0 ? "{}" : arguments));
        }

        return new ChatCompletion(content, calls);
    }

    private static string BuildCompletionsUrl(CopilotSettings settings)
    {
        string baseUrl = settings.BaseUrl.TrimEnd('/');

        if (settings.Provider == CopilotProvider.AzureOpenAI)
        {
            string deployment = string.IsNullOrWhiteSpace(settings.AzureDeployment)
                ? settings.Model
                : settings.AzureDeployment;
            return $"{baseUrl}/{deployment}/chat/completions?api-version=2024-02-15-preview";
        }

        return $"{baseUrl}/chat/completions";
    }

    private static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.TryGetProperty("message", out var message) ? message.GetString() : error.ToString();
        }
        catch (JsonException)
        {
            // Body was not JSON or lacked an error envelope — caller shows raw body.
        }

        return null;
    }
}
