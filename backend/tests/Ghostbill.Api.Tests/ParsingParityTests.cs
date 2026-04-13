using System.Text;
using ClosedXML.Excel;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Shared;

namespace Ghostbill.Api.Tests;

public sealed class ParsingParityTests
{
    private readonly CsvFileParserAdapter _csvParser;
    private readonly ExcelParsingService _excelParser;
    private readonly JsonParsingService _jsonParser;
    private readonly PdfParsingService _pdfParser;

    public ParsingParityTests()
    {
        var valueParsingService = new ValueParsingService();
        var headerDetectionService = new HeaderDetectionService();
        var columnMappingService = new ColumnMappingService();
        var rowMaterializationService = new RowMaterializationService(valueParsingService);

        _csvParser = new CsvFileParserAdapter(new Ghostbill.Api.Services.CsvParsingService());
        _excelParser = new ExcelParsingService(headerDetectionService, columnMappingService, rowMaterializationService);
        _jsonParser = new JsonParsingService(valueParsingService);
        _pdfParser = new PdfParsingService(rowMaterializationService);
    }

    [Fact]
    public void CsvExcelAndJsonProduceEquivalentTransactions()
    {
        var csvTransactions = Parse(_csvParser, BuildCsvFixture(), "sample.csv");
        var excelTransactions = Parse(_excelParser, BuildExcelFixture(), "sample.xlsx");
        var jsonTransactions = Parse(_jsonParser, BuildJsonFixture(), "sample.json");

        Assert.Equal(Normalize(csvTransactions), Normalize(excelTransactions));
        Assert.Equal(Normalize(csvTransactions), Normalize(jsonTransactions));
    }

    [Fact]
    public void PdfParsingIsDeterministicForFixtureFiles()
    {
        var pdf = BuildPdfFixture();

        var firstPass = Parse(_pdfParser, pdf, "sample.pdf");
        var secondPass = Parse(_pdfParser, pdf, "sample.pdf");

        Assert.Equal(Normalize(firstPass), Normalize(secondPass));
        Assert.Equal(6, firstPass.Count);
    }

    private static IReadOnlyList<Transaction> Parse(ITransactionFileParser parser, byte[] bytes, string fileName)
    {
        using var stream = new MemoryStream(bytes);
        return parser.Parse(stream, fileName);
    }

    private static string[] Normalize(IReadOnlyList<Transaction> transactions) =>
        transactions
            .OrderBy(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Description, StringComparer.OrdinalIgnoreCase)
            .Select(transaction => $"{transaction.Date:yyyy-MM-dd}|{transaction.Description}|{transaction.Amount:0.00}")
            .ToArray();

    private static byte[] BuildCsvFixture()
    {
        const string csv = """
date,description,amount
2026-01-05,Netflix,-15.99
2026-02-05,Netflix,-15.99
2026-03-05,Netflix,-15.99
2026-01-10,City Utilities,-82.15
2026-02-10,City Utilities,-84.20
2026-03-10,City Utilities,-79.80
2026-03-12,Payroll,2500.00
""";

        return Encoding.UTF8.GetBytes(csv);
    }

    private static byte[] BuildJsonFixture()
    {
        const string json = """
[
  { "date": "2026-01-05", "description": "Netflix", "amount": -15.99 },
  { "date": "2026-02-05", "description": "Netflix", "amount": -15.99 },
  { "date": "2026-03-05", "description": "Netflix", "amount": -15.99 },
  { "date": "2026-01-10", "description": "City Utilities", "amount": -82.15 },
  { "date": "2026-02-10", "description": "City Utilities", "amount": -84.20 },
  { "date": "2026-03-10", "description": "City Utilities", "amount": -79.80 },
  { "date": "2026-03-12", "description": "Payroll", "amount": 2500.00 }
]
""";

        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] BuildExcelFixture()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Transactions");
        worksheet.Cell(1, 1).Value = "date";
        worksheet.Cell(1, 2).Value = "description";
        worksheet.Cell(1, 3).Value = "amount";

        var rows = new object[,]
        {
            { "2026-01-05", "Netflix", -15.99m },
            { "2026-02-05", "Netflix", -15.99m },
            { "2026-03-05", "Netflix", -15.99m },
            { "2026-01-10", "City Utilities", -82.15m },
            { "2026-02-10", "City Utilities", -84.20m },
            { "2026-03-10", "City Utilities", -79.80m },
            { "2026-03-12", "Payroll", 2500.00m }
        };

        for (var rowIndex = 0; rowIndex < rows.GetLength(0); rowIndex++)
        {
            worksheet.Cell(rowIndex + 2, 1).Value = rows[rowIndex, 0]?.ToString() ?? string.Empty;
            worksheet.Cell(rowIndex + 2, 2).Value = rows[rowIndex, 1]?.ToString() ?? string.Empty;
            worksheet.Cell(rowIndex + 2, 3).Value = Convert.ToDecimal(rows[rowIndex, 2], System.Globalization.CultureInfo.InvariantCulture);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdfFixture() => SamplePdfFactory.CreateStatementPdf(
    [
        new Transaction { Date = new DateTime(2026, 1, 5), Description = "Netflix", Amount = -15.99m },
        new Transaction { Date = new DateTime(2026, 2, 5), Description = "Netflix", Amount = -15.99m },
        new Transaction { Date = new DateTime(2026, 3, 5), Description = "Netflix", Amount = -15.99m },
        new Transaction { Date = new DateTime(2026, 1, 10), Description = "City Utilities", Amount = -82.15m },
        new Transaction { Date = new DateTime(2026, 2, 10), Description = "City Utilities", Amount = -84.20m },
        new Transaction { Date = new DateTime(2026, 3, 10), Description = "City Utilities", Amount = -79.80m }
    ]);
}
