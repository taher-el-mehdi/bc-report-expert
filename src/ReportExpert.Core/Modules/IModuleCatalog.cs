namespace ReportExpert.Core.Modules;

public interface IModuleCatalog
{
    IReadOnlyList<IReportExpertModule> Modules { get; }
    void Register(IReportExpertModule module);
}
