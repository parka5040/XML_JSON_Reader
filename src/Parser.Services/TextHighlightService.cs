using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia.Media;
using System.Text.RegularExpressions;

namespace Parser.Services;

public class SearchHighlightTransformer : DocumentColorizingTransformer
{
    private readonly List<(int startOffset, int length)> _highlights = [];
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(255, 236, 143), 0.3);

    public void SetHighlights(IEnumerable<(int startOffset, int length)> highlights)
    {
        _highlights.Clear();
        _highlights.AddRange(highlights);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var highlight in _highlights)
        {
            if (highlight.startOffset <= line.EndOffset && highlight.startOffset + highlight.length >= line.Offset)
            {
                var startOffset = Math.Max(highlight.startOffset, line.Offset);
                var endOffset = Math.Min(highlight.startOffset + highlight.length, line.EndOffset);
                ChangeLinePart(
                    startOffset,
                    endOffset,
                    element => element.TextRunProperties.SetBackgroundBrush(HighlightBrush));
            }
        }
    }
}

public class TextHighlightService
{
    private readonly SearchHighlightTransformer _transformer;

    public TextHighlightService()
    {
        _transformer = new SearchHighlightTransformer();
    }

    public SearchHighlightTransformer GetTransformer() => _transformer;

    public void HighlightMatches(string text, Regex searchPattern)
    {
        var highlights = new List<(int, int)>();
        var matches = searchPattern.Matches(text);

        foreach (var match in matches.Cast<Match>())
        {
            highlights.Add((match.Index, match.Length));
        }

        _transformer.SetHighlights(highlights);
    }

    public void ClearHighlights()
    {
        _transformer.SetHighlights(new List<(int, int)>());
    }
}