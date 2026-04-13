using ClosedXML.Excel;
using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Shared;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed class ExcelParsingService(
    HeaderDetectionService headerDetectionService,
    ColumnMappingService columnMappingService,
    RowMaterializationService rowMaterializationService) : ITransactionFileParser
{
    public bool CanHandle(string extension) =>
        string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Transaction> Parse(Stream stream, string fileName)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var range = worksheet.RangeUsed();

            if (range is null)
            {
                return [];
            }

            var rows = range.RowsUsed()
                .Select(row => (IReadOnlyList<string>)row.Cells(1, range.ColumnCount())
                    .Select(cell => cell.GetFormattedString())
                    .ToArray())
                .ToArray();

            var headerRowIndex = headerDetectionService.DetectHeaderRow(rows);
            var mappingSourceRow = headerRowIndex is null ? rows[0] : rows[headerRowIndex.Value];
            var mapping = columnMappingService.MapColumns(mappingSourceRow, headerRowIndex is not null);
            var startIndex = headerRowIndex is null ? 0 : headerRowIndex.Value + 1;

            return rowMaterializationService.MaterializeRows(rows, mapping, startIndex).Transactions;
        }
        catch (ParsingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ParsingException("PARSE_ERROR", "Unable to parse the uploaded XLSX file.", exception.Message);
        }
    }
}
