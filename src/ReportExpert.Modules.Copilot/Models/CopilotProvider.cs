namespace ReportExpert.Modules.Copilot.Models;

/// <summary>Supported AI backend providers (all use OpenAI-compatible chat completions).</summary>
public enum CopilotProvider
{
    Groq,
    Gemini,
    Mistral,
    OpenRouter,
    Claude,
    OpenAI,
    AzureOpenAI,
    Ollama
}

/// <summary>A model the settings UI offers for a provider.</summary>
/// <param name="DisplayName">Short label for the combo box.</param>
/// <param name="ModelId">The id sent to the API.</param>
/// <param name="BestFor">One-line guidance under the id.</param>
public sealed record AiModelOption(string DisplayName, string ModelId, string BestFor);

public static class CopilotProviderDefaults
{
    public static string DefaultBaseUrl(CopilotProvider provider) => provider switch
    {
        CopilotProvider.Groq => "https://api.groq.com/openai/v1",
        CopilotProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/openai",
        CopilotProvider.Mistral => "https://api.mistral.ai/v1",
        CopilotProvider.OpenRouter => "https://openrouter.ai/api/v1",
        CopilotProvider.Claude => "https://api.anthropic.com/v1",
        CopilotProvider.OpenAI => "https://api.openai.com/v1",
        CopilotProvider.AzureOpenAI => "https://YOUR-RESOURCE.openai.azure.com/openai/deployments",
        CopilotProvider.Ollama => "http://localhost:11434/v1",
        _ => "https://api.groq.com/openai/v1"
    };

    public static string DefaultModel(CopilotProvider provider) => provider switch
    {
        CopilotProvider.Groq => "llama-3.3-70b-versatile",
        CopilotProvider.Gemini => "gemini-2.5-flash",
        CopilotProvider.Mistral => "mistral-large-latest",
        CopilotProvider.OpenRouter => "anthropic/claude-sonnet-4",
        CopilotProvider.Claude => "claude-sonnet-4-6",
        CopilotProvider.OpenAI => "gpt-4o-mini",
        CopilotProvider.AzureOpenAI => "gpt-4o-mini",
        CopilotProvider.Ollama => "llama3.2",
        _ => "llama-3.3-70b-versatile"
    };

    public static string DisplayName(CopilotProvider provider) => provider switch
    {
        CopilotProvider.Groq => "Groq",
        CopilotProvider.Gemini => "Google Gemini",
        CopilotProvider.Mistral => "Mistral",
        CopilotProvider.OpenRouter => "OpenRouter",
        CopilotProvider.Claude => "Claude (Anthropic)",
        CopilotProvider.OpenAI => "OpenAI",
        CopilotProvider.AzureOpenAI => "Azure OpenAI",
        CopilotProvider.Ollama => "Ollama (local)",
        _ => provider.ToString()
    };

    /// <summary>Where the user creates an API key for this provider.</summary>
    public static string ApiKeyUrl(CopilotProvider provider) => provider switch
    {
        CopilotProvider.Groq => "https://console.groq.com/keys",
        CopilotProvider.Gemini => "https://aistudio.google.com/apikey",
        CopilotProvider.Mistral => "https://console.mistral.ai/api-keys",
        CopilotProvider.OpenRouter => "https://openrouter.ai/keys",
        CopilotProvider.Claude => "https://console.anthropic.com/settings/keys",
        CopilotProvider.OpenAI => "https://platform.openai.com/api-keys",
        CopilotProvider.AzureOpenAI => "https://portal.azure.com/",
        CopilotProvider.Ollama => "https://ollama.com/",
        _ => "https://console.groq.com/keys"
    };

    /// <summary>Models shown in Settings for the given provider.</summary>
    public static IReadOnlyList<AiModelOption> Models(CopilotProvider provider) => provider switch
    {
        CopilotProvider.Gemini =>
        [
            new("Gemini 2.5 Flash", "gemini-2.5-flash", "Fast, strong tool calling — best default for Rex"),
            new("Gemini 2.5 Pro", "gemini-2.5-pro", "Higher quality reasoning and complex edits"),
            new("Gemini 2.5 Flash Lite", "gemini-2.5-flash-lite", "Cheapest / fastest Gemini for simple asks"),
            new("Gemini 2.0 Flash", "gemini-2.0-flash", "Previous-generation Flash"),
            new("Gemini 3.6 Flash", "gemini-3.6-flash", "Latest Flash from Google’s OpenAI-compat docs"),
        ],
        CopilotProvider.Mistral =>
        [
            new("Mistral Large", "mistral-large-latest", "Best quality — strong tool calling for Rex"),
            new("Mistral Medium", "mistral-medium-latest", "Balanced speed and quality"),
            new("Mistral Small", "mistral-small-latest", "Fast and inexpensive"),
            new("Codestral", "codestral-latest", "Code-oriented edits and explanations"),
            new("Ministral 8B", "ministral-8b-latest", "Smallest / cheapest Mistral"),
        ],
        CopilotProvider.OpenRouter =>
        [
            new("Claude Sonnet 4", "anthropic/claude-sonnet-4", "Strong agents via OpenRouter (recommended)"),
            new("Claude 3.5 Sonnet", "anthropic/claude-3.5-sonnet", "Proven tool use, widely available"),
            new("Gemini 2.5 Flash", "google/gemini-2.5-flash", "Fast Gemini routed through OpenRouter"),
            new("GPT-4o mini", "openai/gpt-4o-mini", "Inexpensive OpenAI via OpenRouter"),
            new("Mistral Large", "mistralai/mistral-large", "Mistral via OpenRouter"),
            new("Llama 3.3 70B", "meta-llama/llama-3.3-70b-instruct", "Open-weight model via OpenRouter"),
        ],
        CopilotProvider.Claude =>
        [
            new("Claude Sonnet 4.6", "claude-sonnet-4-6", "Best default for Rex — fast and tool-capable"),
            new("Claude Opus 4.6", "claude-opus-4-6", "Highest quality reasoning and complex edits"),
            new("Claude Sonnet 4", "claude-sonnet-4-20250514", "Stable Sonnet 4 snapshot"),
            new("Claude Haiku 4.5", "claude-haiku-4-5-20251001", "Fastest / cheapest Claude"),
            new("Claude Opus 5", "claude-opus-5", "Latest Opus from Anthropic’s OpenAI-compat docs"),
        ],
        CopilotProvider.OpenAI =>
        [
            new("GPT-4o mini", "gpt-4o-mini", "Fast and inexpensive general-purpose"),
            new("GPT-4o", "gpt-4o", "Higher quality reasoning and coding"),
            new("GPT-4.1 mini", "gpt-4.1-mini", "Strong tool use at lower cost"),
            new("o4-mini", "o4-mini", "Reasoning-oriented mini model"),
        ],
        CopilotProvider.AzureOpenAI =>
        [
            new("GPT-4o mini", "gpt-4o-mini", "Matches your Azure deployment name unless overridden"),
            new("GPT-4o", "gpt-4o", "Higher quality Azure deployment"),
        ],
        CopilotProvider.Ollama =>
        [
            new("Llama 3.2", "llama3.2", "Local default"),
            new("Llama 3.1", "llama3.1", "Local larger Llama"),
            new("Qwen 2.5", "qwen2.5", "Local coding-friendly model"),
            new("Mistral", "mistral", "Local general-purpose"),
        ],
        _ => // Groq and fallback
        [
            new("OpenAI GPT-OSS 120B", "openai/gpt-oss-120b", "High-quality reasoning, coding, agents"),
            new("OpenAI GPT-OSS 20B", "openai/gpt-oss-20b", "Fast, inexpensive general-purpose tasks"),
            new("Llama 3.3 70B", "llama-3.3-70b-versatile", "Chat, coding, long-context applications"),
            new("Llama 3.1 8B", "llama-3.1-8b-instant", "Ultra-fast responses and lightweight workloads"),
        ],
    };

    /// <summary>All providers shown in the Settings combo box.</summary>
    public static IReadOnlyList<CopilotProvider> AllProviders { get; } =
    [
        CopilotProvider.Groq,
        CopilotProvider.Gemini,
        CopilotProvider.Mistral,
        CopilotProvider.OpenRouter,
        CopilotProvider.Claude,
        CopilotProvider.OpenAI,
        CopilotProvider.AzureOpenAI,
        CopilotProvider.Ollama,
    ];
}
