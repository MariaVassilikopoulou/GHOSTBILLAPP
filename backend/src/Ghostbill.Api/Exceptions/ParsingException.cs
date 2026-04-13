namespace Ghostbill.Api.Exceptions;

public sealed class ParsingException(string code, string message, string? details = null) : Exception(message)
{
    public string Code { get; } = code;

    public string? Details { get; } = details;
}
