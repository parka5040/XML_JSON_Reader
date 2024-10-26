using Parser.Core.Models;
using Parser.Core.Enums;

namespace Parser.Core.Interfaces;

public interface IDocumentParser
{
    bool CanParse(string content);
    Task<DocumentNode> ParseAsync(string content);
    string Format(string content);
    DocumentFormat GetDocumentFormat(string content);
}