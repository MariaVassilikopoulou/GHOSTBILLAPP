using System.Text;
using Ghostbill.Api.Controllers;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ghostbill.Api.Tests;

public sealed class TransactionsControllerTests
{
    [Fact]
    public async Task Analyze_ReturnsAnalysisResult_ForSupportedCsvUpload()
    {
        var controller = CreateController();
        var request = new AnalyzeTransactionsRequest
        {
            File = CreateFormFile(
                """
date,description,amount
2026-01-05,Netflix,-15.99
2026-02-05,Netflix,-15.99
2026-03-05,Netflix,-15.99
2026-01-10,City Utilities,-82.15
2026-02-10,City Utilities,-84.20
2026-03-10,City Utilities,-79.80
2026-03-12,Payroll,2500.00
""",
                "transactions.csv")
        };

        var result = await controller.Analyze(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AnalysisResult>(okResult.Value);
        Assert.Single(payload.Ghosts);
        Assert.Single(payload.Regulars);
        Assert.Equal(6, payload.TotalTransactionsAnalyzed);
    }

    [Fact]
    public async Task Analyze_ReturnsUnsupportedFormatError_ForUnknownExtension()
    {
        var controller = CreateController();
        var request = new AnalyzeTransactionsRequest
        {
            File = CreateFormFile("hello world", "transactions.txt")
        };

        var result = await controller.Analyze(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("UNSUPPORTED_FORMAT", payload.Code);
    }

    [Fact]
    public async Task Analyze_ReturnsInvalidFileError_ForEmptyUpload()
    {
        var controller = CreateController();
        var request = new AnalyzeTransactionsRequest
        {
            File = CreateFormFile(string.Empty, "transactions.csv")
        };

        var result = await controller.Analyze(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_FILE", payload.Code);
    }

    private static TransactionsController CreateController()
    {
        var valueParsingService = new ValueParsingService();
        var headerDetectionService = new HeaderDetectionService();
        var columnMappingService = new ColumnMappingService();
        var rowMaterializationService = new RowMaterializationService(valueParsingService);

        var parserResolutionService = new ParserResolutionService(
        [
            new CsvFileParserAdapter(new CsvParsingService()),
            new ExcelParsingService(headerDetectionService, columnMappingService, rowMaterializationService),
            new JsonParsingService(valueParsingService),
            new PdfParsingService(rowMaterializationService)
        ]);

        return new TransactionsController(parserResolutionService, new RecurringExpenseAnalysisService());
    }

    private static IFormFile CreateFormFile(string content, string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }
}
