using Parser.Core.Interfaces;
using Parser.Services.Parsers;

namespace Parser.Services;

public class ParserFactory
{
    private readonly IEnumerable<IDocumentParser> _parsers;

    public ParserFactory()
    {
        _parsers = new IDocumentParser[]
        {
            new JsonDocumentParser(),
            new XmlDocumentParser()
        };
    }

    public IDocumentParser GetParser(string content)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(content));
        if (parser == null)
        {
            throw new InvalidOperationException("No suitable parser found for the given content");
        }
        return parser;
    }
}