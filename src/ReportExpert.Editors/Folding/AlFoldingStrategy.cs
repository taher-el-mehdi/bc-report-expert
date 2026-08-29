using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace ReportExpert.Editors.Folding;

/// <summary>Brace-based folding for AL report blocks. Extensible for dataset/symbol navigation later.</summary>
public sealed class AlFoldingStrategy
{
    private static readonly string[] FoldKeywords =
    [
        "dataset", "dataitem", "procedure", "trigger", "requestpage", "rendering", "labels"
    ];

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        if (document.TextLength == 0)
        {
            return foldings;
        }

        var stack = new Stack<(int Offset, string Keyword)>();

        for (int lineNumber = 1; lineNumber <= document.LineCount; lineNumber++)
        {
            DocumentLine line = document.GetLineByNumber(lineNumber);
            string text = document.GetText(line);

            int searchFrom = 0;
            while (TryFindKeyword(text, searchFrom, out int keywordIndex, out string keyword))
            {
                int absoluteOffset = line.Offset + keywordIndex;
                if (IsBlockOpenAt(document, absoluteOffset))
                {
                    stack.Push((absoluteOffset, keyword));
                }

                searchFrom = keywordIndex + keyword.Length;
            }

            int endIndex = text.IndexOf('}');
            while (endIndex >= 0 && stack.Count > 0)
            {
                var (startOffset, keyword) = stack.Pop();
                int endOffset = line.Offset + endIndex + 1;
                if (endOffset > startOffset)
                {
                    foldings.Add(new NewFolding(startOffset, endOffset)
                    {
                        Name = keyword,
                        DefaultClosed = false
                    });
                }

                endIndex = text.IndexOf('}', endIndex + 1);
            }
        }

        return foldings.OrderBy(f => f.StartOffset);
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateNewFoldings(document);
        manager.UpdateFoldings(foldings, -1);
    }

    private static bool TryFindKeyword(string lineText, int startIndex, out int keywordIndex, out string keyword)
    {
        foreach (string foldKeyword in FoldKeywords)
        {
            keywordIndex = lineText.IndexOf(foldKeyword, startIndex, StringComparison.OrdinalIgnoreCase);
            if (keywordIndex < 0)
            {
                continue;
            }

            if (keywordIndex > 0 && char.IsLetterOrDigit(lineText[keywordIndex - 1]))
            {
                continue;
            }

            int after = keywordIndex + foldKeyword.Length;
            if (after < lineText.Length && char.IsLetterOrDigit(lineText[after]))
            {
                continue;
            }

            keyword = foldKeyword;
            return true;
        }

        keywordIndex = -1;
        keyword = string.Empty;
        return false;
    }

    private static bool IsBlockOpenAt(TextDocument document, int keywordOffset)
    {
        int braceIndex = document.Text.IndexOf('{', keywordOffset, Math.Min(120, document.TextLength - keywordOffset));
        return braceIndex >= 0;
    }
}
