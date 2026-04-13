using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Features;

namespace Ghostbill.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[EnableRateLimiting("upload")]
public sealed class TransactionsController(
    ParserResolutionService parserResolutionService,
    IRecurringExpenseAnalysisService analysisService,
    IOptions<FormOptions> formOptions) : ControllerBase
{
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AnalysisResult>> Analyze([FromForm] AnalyzeTransactionsRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "A file upload is required.",
                Code = "INVALID_FILE"
            });
        }

        if (request.File.Length == 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Uploaded file is empty.",
                Code = "INVALID_FILE"
            });
        }

        var maxBytes = formOptions.Value.MultipartBodyLengthLimit;
        if (request.File.Length > maxBytes)
        {
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge, new ApiErrorResponse
            {
                Message = $"File exceeds the {maxBytes / 1024 / 1024} MB size limit.",
                Code = "INVALID_FILE"
            });
        }

        try
        {
            await using var stream = request.File.OpenReadStream();
            var extension = Path.GetExtension(request.File.FileName);
            var parser = parserResolutionService.Resolve(extension);
            var transactions = parser.Parse(stream, request.File.FileName);

            if (transactions.Count == 0)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Message = "No transaction rows were found in the uploaded file.",
                    Code = "NO_DATA_FOUND"
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Ok(analysisService.Analyze(transactions));
        }
        catch (ParsingException exception)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = exception.Message,
                Code = exception.Code,
                Details = exception.Details
            });
        }
        catch (Exception exception)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Ghostbill could not analyze that file.",
                Code = "PARSE_ERROR",
                Details = exception.Message
            });
        }
    }
}

public sealed class AnalyzeTransactionsRequest
{
    public IFormFile? File { get; init; }
}
