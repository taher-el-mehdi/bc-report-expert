using Microsoft.Extensions.DependencyInjection;
using ReportExpert.Core.Shell;

namespace ReportExpert.Core.Modules;

public interface IReportExpertModule
{
    string Id { get; }
    string DisplayName { get; }
    Version Version { get; }
    bool IsEnabled { get; }
    void ConfigureServices(IServiceCollection services);
    void ConfigureShell(IShellBuilder shell);
}
