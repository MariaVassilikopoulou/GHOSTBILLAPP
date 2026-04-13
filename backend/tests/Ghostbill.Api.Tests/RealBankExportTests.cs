using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;

namespace Ghostbill.Api.Tests;

public sealed class RealBankExportTests
{
    private readonly CsvFileParserAdapter _csvParser;
    private readonly ExcelParsingService _excelParser;
    private readonly PdfParsingService _pdfParser;
    private readonly RecurringExpenseAnalysisService _analysisService = new();

    public RealBankExportTests()
    {
        var valueParsingService = new ValueParsingService();
        var headerDetectionService = new HeaderDetectionService();
        var columnMappingService = new ColumnMappingService();
        var rowMaterializationService = new RowMaterializationService(valueParsingService);

        _csvParser = new CsvFileParserAdapter(new CsvParsingService());
        _excelParser = new ExcelParsingService(headerDetectionService, columnMappingService, rowMaterializationService);
        _pdfParser = new PdfParsingService(rowMaterializationService);
    }

    [Fact]
    public void CsvBankExport_ParsesAndProducesRecurringExpenseGroups()
    {
        using var stream = File.OpenRead(GetDataPath("Transaktioner_2026-04-06_14-52-08.csv"));

        var transactions = _csvParser.Parse(stream, "Transaktioner_2026-04-06_14-52-08.csv");
        var analysis = _analysisService.Analyze(transactions);

        Assert.True(transactions.Count > 600);
        Assert.NotEmpty(analysis.Ghosts);
        Assert.NotEmpty(analysis.Regulars);
    }

    [Fact]
    public void XlsxBankExport_ParsesAndProducesRecurringExpenseGroups()
    {
        using var stream = File.OpenRead(GetDataPath("Transaktioner_2026-04-06_14-52-13.xlsx"));

        var transactions = _excelParser.Parse(stream, "Transaktioner_2026-04-06_14-52-13.xlsx");
        var analysis = _analysisService.Analyze(transactions);

        Assert.True(transactions.Count > 600);
        Assert.NotEmpty(analysis.Ghosts);
        Assert.NotEmpty(analysis.Regulars);
    }

    [Fact]
    public void PdfBankExport_ParsesAndProducesRecurringExpenseGroups()
    {
        using var stream = File.OpenRead(GetDataPath("Transaktioner_2026-04-06_14-52-03.pdf"));

        var transactions = _pdfParser.Parse(stream, "Transaktioner_2026-04-06_14-52-03.pdf");
        var analysis = _analysisService.Analyze(transactions);

        Assert.True(transactions.Count > 600);
        Assert.NotEmpty(analysis.Ghosts);
        Assert.NotEmpty(analysis.Regulars);
    }

    [Fact]
    public void MockSequentialPdf_ParsesAndProducesTransactions()
    {
        using var stream = File.OpenRead(GetDataPath("mock_transactions_sek.pdf"));

        var transactions = _pdfParser.Parse(stream, "mock_transactions_sek.pdf");
        var analysis = _analysisService.Analyze(transactions);

        Assert.True(transactions.Count > 150);
        Assert.True(analysis.TotalTransactionsAnalyzed > 0);
    }

    private static string GetDataPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Ghostbill.slnx")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new DirectoryNotFoundException("Could not locate the workspace root from the test output directory.");
        }

        return Path.Combine(current.FullName, "data", fileName);
    }
}
