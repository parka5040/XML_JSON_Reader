using Parser.Core.Models;

namespace Parser.Services;

public class ParserService
{
    private readonly ParserFactory _factory;

    public ParserService()
    {
        _factory = new ParserFactory();
    }

    public async Task<DocumentNode> ParseDocumentAsync(string content)
    {
        var parser = _factory.GetParser(content);
        return await parser.ParseAsync(content);
    }

    public string FormatDocument(string content)
    {
        var parser = _factory.GetParser(content);
        return parser.Format(content);
    }

    public DocumentFormat GetDocumentFormat(string content)
    {
        try
        {
            var parser = _factory.GetParser(content);
            return parser.GetDocumentFormat(content);
        }
        catch
        {
            return DocumentFormat.Unknown;
        }
    }
}