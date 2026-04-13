namespace Ghostbill.Api.Parsing.Shared;

public sealed class HeaderDetectionService
{
    public int? DetectHeaderRow(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var maxRows = Math.Min(rows.Count, 20);

        for (var index = 0; index < maxRows; index++)
        {
            var row = rows[index];
            var normalized = row.Select(HeaderNormalization.Normalize).ToArray();

            var distinctHits = 0;
            distinctHits += normalized.Any(value => ParsingAliases.Date.Contains(value)) ? 1 : 0;
            distinctHits += normalized.Any(value => ParsingAliases.Description.Contains(value)) ? 1 : 0;
            distinctHits += normalized.Any(value => ParsingAliases.Amount.Contains(value)) ? 1 : 0;

            if (distinctHits >= 2)
            {
                return index;
            }
        }

        return null;
    }
}
