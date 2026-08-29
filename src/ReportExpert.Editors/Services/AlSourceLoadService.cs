using System.IO;
using ReportExpert.Core.Editors;

namespace ReportExpert.Editors.Services;

public sealed class AlSourceLoadService : IAlSourceLoadService
{
    public async Task<AlSourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AlSourceDocument.NotFound(path);
        }

        if (!File.Exists(path))
        {
            return AlSourceDocument.NotFound(path);
        }

        try
        {
            string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return AlSourceDocument.FromContent(path, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AlSourceDocument
            {
                FilePath = path,
                Content = string.Empty,
                LineCount = 0,
                Exists = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
