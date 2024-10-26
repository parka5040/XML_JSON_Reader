using System.Xml.Linq;
using Parser.Core.Models;
using Parser.Core.Interfaces;

namespace Parser.Services.Parsers;

public class XmlDocumentParser : IDocumentParser
{
    public bool CanParse(string content)
    {
        try
        {
            XDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DocumentNode> ParseAsync(string content)
    {
        var doc = XDocument.Parse(content);
        return await Task.Run(() => ParseElement(doc.Root!));
    }

    private static DocumentNode ParseElement(XElement element)
    {
        var node = new DocumentNode
        {
            Name = element.Name.LocalName,
            Type = "Element",
            Value = element.HasElements ? string.Empty : element.Value
        };

        // Add attributes
        foreach (var attribute in element.Attributes())
        {
            node.Attributes[attribute.Name.LocalName] = attribute.Value;
        }

        // Add child elements
        foreach (var childElement in element.Elements())
        {
            node.Children.Add(ParseElement(childElement));
        }

        return node;
    }

    public string Format(string content)
    {
        var doc = XDocument.Parse(content);
        return doc.ToString(SaveOptions.None);
    }

    public DocumentFormat GetDocumentFormat(string content) => DocumentFormat.Xml;
}