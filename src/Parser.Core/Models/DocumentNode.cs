using System.Collections.ObjectModel;

namespace Parser.Core.Models;

public class DocumentNode
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public ObservableCollection<DocumentNode> Children { get; } = [];
    public Dictionary<string, string> Attributes { get; } = [];

    public override string ToString() => $"{Name}: {Value}";
}