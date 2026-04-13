using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Shared;
using System.Text;

namespace Ghostbill.Api.Services;

public sealed class CsvParsingService
{
    private readonly HeaderDetectionService _headerDetectionService = new();
    private readonly ColumnMappingService _columnMappingService = new();
    private readonly RowMaterializationService _rowMaterializationService;
    private readonly ValueParsingService _valueParsingService = new();

    public CsvParsingService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _rowMaterializationService = new RowMaterializationService(_valueParsingService);
    }

    public IReadOnlyList<Transaction> Parse(Stream stream)
    {
        var content = ReadContent(stream);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ParsingException("INVALID_FILE", "Uploaded file is empty.");
        }

        var delimiter = DetectDelimiter(content);
        var rows = ParseRows(content, delimiter);
        if (rows.Count == 0)
        {
            return [];
        }

        var headerRowIndex = _headerDetectionService.DetectHeaderRow(rows);
        var mappingSource = headerRowIndex is null ? rows[0] : rows[headerRowIndex.Value];
        var mapping = _columnMappingService.MapColumns(mappingSource, headerRowIndex is not null);
        var startIndex = headerRowIndex is null ? 0 : headerRowIndex.Value + 1;

        return _rowMaterializationService.MaterializeRows(rows, mapping, startIndex).Transactions;
    }

    private static string ReadContent(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static char DetectDelimiter(string content)
    {
        var sampleLines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(6)
            .ToArray();

        var delimiters = new[] { ',', ';', '\t' };
        return delimiters
            .Select(delimiter => new
            {
                Delimiter = delimiter,
                Score = sampleLines.Sum(line => line.Count(character => character == delimiter))
            })
            .OrderByDescending(candidate => candidate.Score)
            .First().Delimiter;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseRows(string content, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var currentValue = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    currentValue.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == delimiter && !inQuotes)
            {
                currentRow.Add(currentValue.ToString());
                currentValue.Clear();
                continue;
            }

            if ((character == '\n' || character == '\r') && !inQuotes)
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                currentRow.Add(currentValue.ToString());
                currentValue.Clear();

                if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    rows.Add(currentRow.ToArray());
                }

                currentRow = [];
                continue;
            }

            currentValue.Append(character);
        }

        if (currentValue.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentValue.ToString());
            if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                rows.Add(currentRow.ToArray());
            }
        }

        return rows;
    }
}
