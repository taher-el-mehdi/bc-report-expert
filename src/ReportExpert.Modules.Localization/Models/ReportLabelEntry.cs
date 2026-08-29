namespace ReportExpert.Modules.Localization.Models;

/// <summary>One caption or label entry from rd:ReportLabels.</summary>
public sealed class ReportLabelEntry
{
    public string LabelName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Language { get; set; } = "default";
}
