using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Configuration;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Load();
    void Save();
}
