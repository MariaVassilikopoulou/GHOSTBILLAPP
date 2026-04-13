using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Services;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed class CsvFileParserAdapter(CsvParsingService csvParsingService) : ITransactionFileParser
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Transaction> Parse(Stream stream, string fileName) =>
        csvParsingService.Parse(stream);
}
