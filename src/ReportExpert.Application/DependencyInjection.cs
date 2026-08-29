using Microsoft.Extensions.DependencyInjection;
using ReportExpert.Application.Reports.DummyData;
using ReportExpert.Application.Reports.Export;
using ReportExpert.Application.Reports.LoadReport;
using ReportExpert.Application.Reports.Preview;

namespace ReportExpert.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<LoadReportUseCase>();
        services.AddSingleton<GenerateDummyDataUseCase>();
        services.AddSingleton<ExportReportUseCase>();
        services.AddSingleton<PreviewReportUseCase>();
        return services;
    }
}
