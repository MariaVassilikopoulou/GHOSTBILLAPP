namespace Ghostbill.Api.Parsing.Shared;

public sealed class ColumnMappingService
{
    public ColumnMapping MapColumns(IReadOnlyList<string> row, bool hasHeader)
    {
        if (!hasHeader)
        {
            if (row.Count < 3)
            {
                throw new InvalidOperationException("At least three columns are required when no header row is present.");
            }

            return new ColumnMapping(0, 1, 2, false);
        }

        var normalized = row.Select(HeaderNormalization.Normalize).ToArray();
        var dateIndex = FindIndex(normalized, ParsingAliases.Date);
        var descriptionIndex = FindIndex(normalized, ParsingAliases.Description);
        var amountIndex = FindIndex(normalized, ParsingAliases.Amount);

        if (dateIndex is null || descriptionIndex is null || amountIndex is null)
        {
            throw new InvalidOperationException("Unable to map the required date, description, and amount columns.");
        }

        return new ColumnMapping(dateIndex.Value, descriptionIndex.Value, amountIndex.Value, true);
    }

    private static int? FindIndex(IReadOnlyList<string> header, IReadOnlyCollection<string> aliases)
    {
        foreach (var alias in aliases)
        {
            for (var index = 0; index < header.Count; index++)
            {
                if (string.Equals(header[index], alias, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return null;
    }
}

public sealed record ColumnMapping(int DateIndex, int DescriptionIndex, int AmountIndex, bool HasHeader);
