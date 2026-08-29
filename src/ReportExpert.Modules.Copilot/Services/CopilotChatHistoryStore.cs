using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReportExpert.Modules.Copilot.Services;

public sealed class CopilotChatMessageDto
{
    public bool IsUser { get; set; }
    public string Content { get; set; } = string.Empty;
}

public sealed class CopilotChatHistoryDto
{
    public string RelativePath { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
    public List<CopilotChatMessageDto> Messages { get; set; } = [];
}

/// <summary>
/// Persists Copilot chat history under <c>{workspace}/.report_expert/chat/</c>,
/// one JSON file per layout (keyed by relative path).
/// </summary>
public sealed class CopilotChatHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string? _workspaceRoot;

    public void SetWorkspaceRoot(string? rootPath)
    {
        _workspaceRoot = string.IsNullOrWhiteSpace(rootPath) ? null : Path.GetFullPath(rootPath);
    }

    public IReadOnlyList<CopilotChatMessageDto> Load(string reportFullPath)
    {
        string? path = GetHistoryFilePath(reportFullPath);
        if (path is null || !File.Exists(path))
            return [];

        try
        {
            var dto = JsonSerializer.Deserialize<CopilotChatHistoryDto>(File.ReadAllText(path), JsonOptions);
            return dto?.Messages?
                       .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                       .ToList()
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(string reportFullPath, IEnumerable<(bool IsUser, string Content)> messages)
    {
        string? path = GetHistoryFilePath(reportFullPath);
        if (path is null || _workspaceRoot is null)
            return;

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string relative = Path.GetRelativePath(_workspaceRoot, reportFullPath).Replace('\\', '/');
            var dto = new CopilotChatHistoryDto
            {
                RelativePath = relative,
                UpdatedUtc = DateTime.UtcNow,
                Messages = messages
                    .Where(m => !string.IsNullOrWhiteSpace(m.Content) &&
                                m.Content is not "Thinking..." &&
                                !m.Content.StartsWith("Error:", StringComparison.Ordinal))
                    .Select(m => new CopilotChatMessageDto { IsUser = m.IsUser, Content = m.Content })
                    .ToList()
            };

            File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // History persistence must never break chat.
        }
    }

    public void Delete(string reportFullPath)
    {
        string? path = GetHistoryFilePath(reportFullPath);
        if (path is null || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private string? GetHistoryFilePath(string reportFullPath)
    {
        if (_workspaceRoot is null || string.IsNullOrWhiteSpace(reportFullPath))
            return null;

        string full = Path.GetFullPath(reportFullPath);
        if (!full.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        string relative = Path.GetRelativePath(_workspaceRoot, full).Replace('\\', '/');
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relative.ToLowerInvariant())))
            .ToLowerInvariant()[..24];
        string safeName = Path.GetFileNameWithoutExtension(reportFullPath);
        foreach (char c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        return Path.Combine(_workspaceRoot, ".report_expert", "chat", $"{safeName}.{hash}.json");
    }
}
