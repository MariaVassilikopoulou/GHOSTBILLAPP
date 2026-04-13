using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Parsing.Abstractions;

namespace Ghostbill.Api.Parsing.Resolution;

public sealed class ParserResolutionService(IEnumerable<ITransactionFileParser> parsers)
{
    private static readonly string[] SupportedExtensions = [".csv", ".xlsx", ".json", ".pdf"];
    private readonly IReadOnlyList<ITransactionFileParser> _parsers = parsers.ToArray();

    public ITransactionFileParser Resolve(string extension)
    {
        var normalizedExtension = Normalize(extension);
        var matches = _parsers.Where(parser => parser.CanHandle(normalizedExtension)).ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ParsingException("UNSUPPORTED_FORMAT", "Unsupported file format.", "Ghostbill supports CSV, XLSX, JSON, and PDF uploads."),
            _ => throw new InvalidOperationException($"Multiple parsers can handle '{normalizedExtension}'.")
        };
    }

    public void ValidateConfiguration()
    {
        foreach (var extension in SupportedExtensions)
        {
            var matches = _parsers.Count(parser => parser.CanHandle(extension));
            if (matches != 1)
            {
                throw new InvalidOperationException($"Parser configuration requires exactly one parser for '{extension}', found {matches}.");
            }
        }
    }

    private static string Normalize(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
}
