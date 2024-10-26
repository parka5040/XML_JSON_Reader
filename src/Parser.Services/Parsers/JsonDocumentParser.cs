using System.Text.Json;
using Parser.Core.Models;
using Parser.Core.Interfaces;

namespace Parser.Services.Parsers;

public class JsonDocumentParser : IDocumentParser
{
    public bool CanParse(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DocumentNode> ParseAsync(string content)
    {
        using var doc = JsonDocument.Parse(content);
        return await Task.Run(() => ParseElement(doc.RootElement));
    }

    private static DocumentNode ParseElement(JsonElement element, string name = "")
    {
        var node = new DocumentNode
        {
            Name = name,
            Type = element.ValueKind.ToString()
        };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childNode = ParseElement(property.Value, property.Name);
                    node.Children.Add(childNode);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var childNode = ParseElement(item, $"[{index}]");
                    node.Children.Add(childNode);
                    index++;
                }
                break;

            case JsonValueKind.String:
                node.Value = $"\"{element.GetString()}\"";
                break;

            case JsonValueKind.Number:
                node.Value = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                node.Value = element.GetBoolean().ToString().ToLower();
                break;

            case JsonValueKind.Null:
                node.Value = "null";
                break;

            default:
                node.Value = element.GetRawText();
                break;
        }

        return node;
    }

    public string Format(string content)
    {
        using var doc = JsonDocument.Parse(content);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        doc.WriteTo(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public DocumentFormat GetDocumentFormat(string content) => DocumentFormat.Json;
}