namespace ReportExpert.Core.Modules;

public sealed class ModuleCatalog : IModuleCatalog
{
    private readonly List<IReportExpertModule> _modules = [];

    public IReadOnlyList<IReportExpertModule> Modules => _modules;

    public void Register(IReportExpertModule module) => _modules.Add(module);
}
