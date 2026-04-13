namespace Ghostbill.Api.Models;

public sealed class ApiErrorResponse
{
    public string Message { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? Details { get; init; }
}
