using System.Text.RegularExpressions;
using Parser.Core.Models;

namespace Parser.Services;

public class SearchOptions
{
    public bool MatchCase { get; set; }
    public bool UseRegex { get; set; }
    public bool SearchInValues { get; set; } = true;
    public bool SearchInNames { get; set; } = true;
    public bool SearchInAttributes { get; set; } = true;
}

public class SearchResult
{
    public DocumentNode Node { get; set; } = null!;
    public string MatchedField { get; set; } = string.Empty;
    public string MatchedValue { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class SearchService
{
    private readonly SearchOptions _options;

    public SearchService(SearchOptions options)
    {
        _options = options;
    }

    private void SearchNode(DocumentNode node, Regex pattern, string currentPath, List<SearchResult> results)
    {
        var nodePath = string.IsNullOrEmpty(currentPath) ? node.Name : $"{currentPath}/{node.Name}";

        if (_options.SearchInNames && IsMatch(node.Name, pattern))
        {
            results.Add(new SearchResult
            {
                Node = node,
                MatchedField = "name",
                MatchedValue = node.Name,
                Path = nodePath
            });
        }

        if (_options.SearchInValues && !string.IsNullOrEmpty(node.Value) && IsMatch(node.Value, pattern))
        {
            results.Add(new SearchResult
            {
                Node = node,
                MatchedField = "value",
                MatchedValue = node.Value,
                Path = nodePath
            });
        }

        if (_options.SearchInAttributes && node.Attributes.Count != 0)
        {
            foreach (var attr in node.Attributes)
            {
                if (IsMatch(attr.Key, pattern) || IsMatch(attr.Value, pattern))
                {
                    results.Add(new SearchResult
                    {
                        Node = node,
                        MatchedField = $"attribute:{attr.Key}",
                        MatchedValue = attr.Value,
                        Path = nodePath
                    });
                }
            }
        }
        foreach (var child in node.Children)
        {
            SearchNode(child, pattern, nodePath, results);
        }
    }

    private static bool IsMatch(string text, Regex pattern)
    {
        return pattern.IsMatch(text);
    }
}
