using ReportExpert.Domain.Documents;

namespace ReportExpert.Core.Documents;

public interface IDocumentManager
{
    IDocument? ActiveDocument { get; }
    IReadOnlyList<IDocument> OpenDocuments { get; }
    event EventHandler? ActiveDocumentChanged;
    Task<IDocument> OpenAsync(string filePath, CancellationToken cancellationToken = default);
    Task CloseAsync(IDocument document, CancellationToken cancellationToken = default);
    void SetActive(IDocument document);
}
