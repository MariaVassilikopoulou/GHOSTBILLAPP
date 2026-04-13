using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── File size limit ────────────────────────────────────────────────────────
var maxFileSizeMb = builder.Configuration.GetValue<int>("Upload:MaxFileSizeMb", 10);
var maxFileSizeBytes = maxFileSizeMb * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maxFileSizeBytes);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maxFileSizeBytes);

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Rate limiting ──────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("upload", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue<int>("RateLimit:RequestsPerMinute", 20);
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Application services ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<CsvParsingService>();
builder.Services.AddSingleton<HeaderDetectionService>();
builder.Services.AddSingleton<ColumnMappingService>();
builder.Services.AddSingleton<ValueParsingService>();
builder.Services.AddSingleton<RowMaterializationService>();
builder.Services.AddSingleton<IRecurringExpenseAnalysisService, RecurringExpenseAnalysisService>();
builder.Services.AddSingleton<ITransactionFileParser, CsvFileParserAdapter>();
builder.Services.AddSingleton<ITransactionFileParser, ExcelParsingService>();
builder.Services.AddSingleton<ITransactionFileParser, JsonParsingService>();
builder.Services.AddSingleton<ITransactionFileParser, PdfParsingService>();
builder.Services.AddSingleton<ParserResolutionService>();

var app = builder.Build();
app.Services.GetRequiredService<ParserResolutionService>().ValidateConfiguration();

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
