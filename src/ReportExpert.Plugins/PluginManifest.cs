namespace ReportExpert.Plugins;

public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EntryAssembly { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
}
