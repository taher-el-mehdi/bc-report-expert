using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Abstractions;

/// <summary>
/// Structure-preserving RDLC load/save.
/// </summary>
public interface IRdlcPersistence
{
    RdlcDocument Load(string path);
    void Save(RdlcDocument document, string? path = null);
}
