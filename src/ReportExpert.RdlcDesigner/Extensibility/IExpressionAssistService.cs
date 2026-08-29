namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Future extension point for expression IntelliSense / validation.
/// Not implemented in the Minimal MVP (Value is plain text).
/// </summary>
public interface IExpressionAssistService
{
    IReadOnlyList<string> Suggest(string prefix);
    bool Validate(string expression, out string? error);
}
