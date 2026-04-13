using Ghostbill.Api.Models;

namespace Ghostbill.Api.Parsing.Shared;

public sealed class RowMaterializationService(ValueParsingService valueParsingService)
{
    public RowMaterializationResult MaterializeRows(IReadOnlyList<IReadOnlyList<string>> rows, ColumnMapping mapping, int startIndex)
    {
        var transactions = new List<Transaction>();
        var skippedReasons = new List<string>();

        for (var index = startIndex; index < rows.Count; index++)
        {
            var row = rows[index];

            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var maxIndex = new[] { mapping.DateIndex, mapping.DescriptionIndex, mapping.AmountIndex }.Max();
            if (row.Count <= maxIndex)
            {
                skippedReasons.Add($"Row {index + 1}: insufficient column count.");
                continue;
            }

            var description = row[mapping.DescriptionIndex].Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                skippedReasons.Add($"Row {index + 1}: missing description.");
                continue;
            }

            try
            {
                transactions.Add(new Transaction
                {
                    Date = valueParsingService.ParseDate(row[mapping.DateIndex]),
                    Description = description,
                    Amount = valueParsingService.ParseAmount(row[mapping.AmountIndex])
                });
            }
            catch (FormatException exception)
            {
                skippedReasons.Add($"Row {index + 1}: {exception.Message}");
            }
        }

        return new RowMaterializationResult(transactions, skippedReasons);
    }
}

public sealed record RowMaterializationResult(IReadOnlyList<Transaction> Transactions, IReadOnlyList<string> SkippedReasons);
