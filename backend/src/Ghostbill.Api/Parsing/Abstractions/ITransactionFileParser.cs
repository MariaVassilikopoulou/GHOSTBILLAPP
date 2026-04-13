using Ghostbill.Api.Models;

namespace Ghostbill.Api.Parsing.Abstractions;

public interface ITransactionFileParser
{
    bool CanHandle(string extension);

    IReadOnlyList<Transaction> Parse(Stream stream, string fileName);
}
