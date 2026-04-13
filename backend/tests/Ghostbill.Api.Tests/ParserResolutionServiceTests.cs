using Ghostbill.Api.Exceptions;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Resolution;

namespace Ghostbill.Api.Tests;

public sealed class ParserResolutionServiceTests
{
    [Fact]
    public void Resolve_ReturnsMatchingParser_ForSupportedExtension()
    {
        var expectedParser = new StubParser(".csv");
        var service = new ParserResolutionService(
        [
            expectedParser,
            new StubParser(".xlsx"),
            new StubParser(".json"),
            new StubParser(".pdf")
        ]);

        var resolved = service.Resolve(".csv");

        Assert.Same(expectedParser, resolved);
    }

    [Fact]
    public void Resolve_ThrowsUnsupportedFormat_ForUnknownExtension()
    {
        var service = new ParserResolutionService(
        [
            new StubParser(".csv"),
            new StubParser(".xlsx"),
            new StubParser(".json"),
            new StubParser(".pdf")
        ]);

        var exception = Assert.Throws<ParsingException>(() => service.Resolve(".txt"));

        Assert.Equal("UNSUPPORTED_FORMAT", exception.Code);
    }

    [Fact]
    public void ValidateConfiguration_Throws_WhenDuplicateParsersHandleTheSameExtension()
    {
        var service = new ParserResolutionService(
        [
            new StubParser(".csv"),
            new StubParser(".csv"),
            new StubParser(".json"),
            new StubParser(".pdf"),
            new StubParser(".xlsx")
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => service.ValidateConfiguration());

        Assert.Contains(".csv", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubParser(string extension) : ITransactionFileParser
    {
        public bool CanHandle(string candidateExtension) =>
            string.Equals(candidateExtension, extension, StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<Transaction> Parse(Stream stream, string fileName) => [];
    }
}
