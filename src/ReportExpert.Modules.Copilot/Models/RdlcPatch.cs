namespace ReportExpert.Modules.Copilot.Models;

/// <summary>A find/replace or full-document XML patch proposed by the agent.</summary>
public sealed class RdlcPatch
{
    public string Description { get; init; } = string.Empty;
    public string? Find { get; init; }
    public string Replace { get; init; } = string.Empty;
    public bool IsFullDocument => string.IsNullOrEmpty(Find);
}
