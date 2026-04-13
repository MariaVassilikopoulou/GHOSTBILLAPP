using System.Text.RegularExpressions;
using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed partial class PdfParsingService(RowMaterializationService rowMaterializationService) : ITransactionFileParser
{
    private const double RowTolerance = 2.5;
    private const double BookingDateLeft = 145;
    private const double BookingDateRight = 205;
    private const double TransactionDateLeft = 205;
    private const double TransactionDateRight = 260;
    private const double ValueDateLeft = 260;
    private const double ValueDateRight = 312;
    private const double DescriptionLeft = 312;
    private const double DescriptionRight = 438;
    private const double AmountLeft = 438;
    private const double AmountRight = 490;

    public bool CanHandle(string extension) =>
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Transaction> Parse(Stream stream, string fileName)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            var rows = new List<IReadOnlyList<string>>();
            var seenRows = new HashSet<string>(StringComparer.Ordinal);

            foreach (var page in document.GetPages())
            {
                var pageRows = ExtractRows(page).ToList();
                if (pageRows.Count == 0)
                {
                    pageRows = ExtractSequentialTableRows(page).ToList();
                }

                if (pageRows.Count == 0)
                {
                    pageRows = ExtractRegexRows(page).ToList();
                }

                foreach (var row in pageRows)
                {
                    var key = string.Join('|', row);
                    if (!seenRows.Add(key))
                    {
                        continue;
                    }

                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                throw new ParsingException("PARSE_ERROR", "Unable to parse the uploaded PDF file.", "No machine-readable transaction rows were found.");
            }

            return rowMaterializationService.MaterializeRows(rows, new ColumnMapping(0, 1, 2, false), 0).Transactions;
        }
        catch (ParsingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ParsingException("PARSE_ERROR", "Unable to parse the uploaded PDF file.", exception.Message);
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ExtractRows(Page page)
    {
        var rowGroups = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Where(word => word.BoundingBox.Left >= BookingDateLeft - 4)
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .ThenBy(word => word.BoundingBox.Left)
            .Aggregate(new List<PdfRow>(), (rows, word) =>
            {
                var bottom = word.BoundingBox.Bottom;
                var row = rows.FirstOrDefault(existing => Math.Abs(existing.Bottom - bottom) <= RowTolerance);
                if (row is null)
                {
                    row = new PdfRow(bottom);
                    rows.Add(row);
                }

                row.Words.Add(word);
                return rows;
            });

        foreach (var row in rowGroups.OrderByDescending(candidate => candidate.Bottom))
        {
            var orderedWords = row.Words.OrderBy(word => word.BoundingBox.Left).ToArray();
            var bookingDate = JoinWords(orderedWords, BookingDateLeft, BookingDateRight, preserveSpacing: true);
            var transactionDate = JoinWords(orderedWords, TransactionDateLeft, TransactionDateRight, preserveSpacing: true);
            var valueDate = JoinWords(orderedWords, ValueDateLeft, ValueDateRight, preserveSpacing: true);
            var description = JoinWords(orderedWords, DescriptionLeft, DescriptionRight, preserveSpacing: true);
            var amount = JoinWords(orderedWords, AmountLeft, AmountRight, preserveSpacing: false);

            var date = FirstNonEmpty(transactionDate, bookingDate, valueDate);
            if (!DatePatternRegex().IsMatch(date) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(amount))
            {
                continue;
            }

            if (LooksLikeHeader(description) || LooksLikeNoise(description))
            {
                continue;
            }

            yield return [date, description, amount];
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ExtractRegexRows(Page page)
    {
        var lines = page.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            foreach (Match match in TransactionLineRegex().Matches(line))
            {
                yield return
                [
                    match.Groups["date"].Value,
                    match.Groups["description"].Value,
                    match.Groups["amount"].Value
                ];
            }
        }

        var flattenedText = WhitespaceRegex().Replace(page.Text, " ").Trim();
        foreach (Match match in TransactionLineRegex().Matches(flattenedText))
        {
            yield return
            [
                match.Groups["date"].Value,
                match.Groups["description"].Value,
                match.Groups["amount"].Value
            ];
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ExtractSequentialTableRows(Page page)
    {
        var text = page.Text.Trim();
        var firstDateMatch = AnyDateRegex().Match(text);
        if (!firstDateMatch.Success)
        {
            yield break;
        }

        var transactionText = text[firstDateMatch.Index..];

        foreach (Match match in SequentialRecordRegex().Matches(transactionText))
        {
            yield return
            [
                match.Groups["date"].Value,
                match.Groups["description"].Value,
                match.Groups["amount"].Value
            ];
        }
    }

    private static bool LooksLikeHeader(string value)
    {
        var normalized = HeaderNormalization.Normalize(value);
        return normalized is "beskrivning" or "referens" or "belopp" or "bokfortsaldo" or "transaktionsdag" or "bokforingsdag" or "valutadag";
    }

    private static bool LooksLikeNoise(string value)
    {
        var normalized = HeaderNormalization.Normalize(value);
        return normalized.StartsWith("saldo", StringComparison.Ordinal)
            || normalized.StartsWith("kontohavare", StringComparison.Ordinal)
            || normalized.StartsWith("privatkonto", StringComparison.Ordinal)
            || normalized.StartsWith("transaktioner", StringComparison.Ordinal)
            || normalized.StartsWith("skapad", StringComparison.Ordinal)
            || normalized == "sek";
    }

    private static string JoinWords(IEnumerable<Word> words, double minLeft, double maxLeft, bool preserveSpacing)
    {
        var selected = words
            .Where(word => word.BoundingBox.Left >= minLeft && word.BoundingBox.Left < maxLeft)
            .OrderBy(word => word.BoundingBox.Left)
            .Select(word => word.Text.Trim())
            .Where(word => word.Length > 0)
            .ToArray();

        if (selected.Length == 0)
        {
            return string.Empty;
        }

        return preserveSpacing ? string.Join(" ", selected) : string.Concat(selected);
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled)]
    private static partial Regex DatePatternRegex();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}", RegexOptions.Compiled)]
    private static partial Regex AnyDateRegex();

    [GeneratedRegex(@"(?<date>\d{4}-\d{2}-\d{2})(?<description>.*?)(?<transactionId>TXN\d+)(?<type>Debit|Credit)(?<amount>[\+\-]?\d[\d,\.]*)(?<currency>[A-Z]{3})(?<balance>[\+\-]?\d[\d,\.]*)(?=(?:\d{4}-\d{2}-\d{2})|$)", RegexOptions.Compiled)]
    private static partial Regex SequentialRecordRegex();

    [GeneratedRegex(@"(?<date>\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})\s+(?<description>[A-Za-z][A-Za-z\s&'\-]+?)\s+(?<amount>\(?-?[$€£]?\d[\d,\.]*\)?)\s*(?=(?:\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})|$)", RegexOptions.Compiled)]
    private static partial Regex TransactionLineRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    private sealed class PdfRow(double bottom)
    {
        public double Bottom { get; } = bottom;

        public List<Word> Words { get; } = [];
    }
}
