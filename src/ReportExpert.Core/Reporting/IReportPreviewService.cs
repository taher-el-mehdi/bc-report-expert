using System.Data;

namespace ReportExpert.Core.Reporting;

public interface IReportPreviewService : IDisposable
{
    int CurrentPage { get; }
    int TotalPages { get; }
    object Viewer { get; }
    void LoadReport(string rdlcPath, IReadOnlyList<DataTable> tables, IDictionary<string, object?>? parameters = null);
    void SetParameters(IDictionary<string, object?> parameters);
    void Refresh();
    void Print();
    void ZoomIn();
    void ZoomOut();
    void FitWidth();
    void FitPage();
    void FirstPage();
    void PreviousPage();
    void NextPage();
    void LastPage();
}
