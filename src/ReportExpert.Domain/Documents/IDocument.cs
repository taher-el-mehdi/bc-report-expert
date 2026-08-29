namespace ReportExpert.Domain.Documents;

public interface IDocument
{
    string Id { get; }
    string Title { get; }
    string FilePath { get; }
    bool IsDirty { get; }
}
