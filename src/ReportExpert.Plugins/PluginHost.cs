using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReportExpert.Core.Modules;
using ReportExpert.Core.Reporting;

namespace ReportExpert.Plugins;

public sealed class PluginHost
{
    private readonly ILogger<PluginHost> _logger;

    public PluginHost(ILogger<PluginHost> logger) => _logger = logger;

    public IReadOnlyList<IReportExpertModule> DiscoverModules(string pluginsDirectory)
    {
        var modules = new List<IReportExpertModule>();
        if (!Directory.Exists(pluginsDirectory))
            return modules;

        foreach (var manifestPath in Directory.EnumerateFiles(pluginsDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath))
                    ?? throw new InvalidDataException("Invalid plugin manifest.");

                string pluginDir = Path.GetDirectoryName(manifestPath)!;
                string assemblyPath = Path.Combine(pluginDir, manifest.EntryAssembly);
                if (!File.Exists(assemblyPath))
                {
                    _logger.LogWarning("Plugin assembly not found: {AssemblyPath}", assemblyPath);
                    continue;
                }

                var context = new PluginLoadContext(assemblyPath);
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
                var moduleType = assembly.GetType(manifest.ModuleType, throwOnError: true)!;
                if (Activator.CreateInstance(moduleType) is IReportExpertModule module)
                    modules.Add(module);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {ManifestPath}", manifestPath);
            }
        }

        return modules;
    }
}

public sealed class ExportProviderRegistry
{
    private readonly List<IExportProvider> _providers = [];

    public ExportProviderRegistry(IEnumerable<IExportProvider> providers) => _providers.AddRange(providers);

    public IReadOnlyList<IExportProvider> Providers => _providers;

    public void Register(IExportProvider provider) => _providers.Add(provider);

    public IExportProvider? Resolve(ReportExpert.Domain.Export.ExportFormat format) =>
        _providers.FirstOrDefault(p => p.Format == format);
}

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) => _resolver = new AssemblyDependencyResolver(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
