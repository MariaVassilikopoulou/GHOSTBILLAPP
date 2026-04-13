using System.Text;
using System.Text.Json;
using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Shared;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed class JsonParsingService(ValueParsingService valueParsingService) : ITransactionFileParser
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Transaction> Parse(Stream stream, string fileName)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            using var document = JsonDocument.Parse(reader.ReadToEnd());
            var root = document.RootElement;

            JsonElement arrayElement = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("transactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array => transactions,
                _ => throw new ParsingException("PARSE_ERROR", "Unsupported JSON structure.", "Expected a top-level array or an object with a transactions array.")
            };

            var transactionsResult = new List<Transaction>();
            foreach (var element in arrayElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new ParsingException("PARSE_ERROR", "Unsupported JSON structure.", "Each transaction entry must be an object.");
                }

                transactionsResult.Add(new Transaction
                {
                    Date = valueParsingService.ParseDate(ReadProperty(element, ParsingAliases.Date)),
                    Description = ReadProperty(element, ParsingAliases.Description),
                    Amount = valueParsingService.ParseAmount(ReadProperty(element, ParsingAliases.Amount))
                });
            }

            return transactionsResult;
        }
        catch (ParsingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ParsingException("PARSE_ERROR", "Unable to parse the uploaded JSON file.", exception.Message);
        }
    }

    private static string ReadProperty(JsonElement element, IReadOnlyCollection<string> aliases)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (aliases.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => property.Value.ToString()
                };
            }
        }

        throw new ParsingException("PARSE_ERROR", "Unsupported JSON structure.", $"Missing required property alias. Expected one of: {string.Join(", ", aliases)}.");
    }
}
