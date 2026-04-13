namespace Ghostbill.Api.Parsing.Shared;

internal static class ParsingAliases
{
    public static readonly string[] Date =
    [
        "transaktionsdag",
        "transactiondate",
        "bokforingsdag",
        "posteddate",
        "date",
        "valutadag",
        "valuedate"
    ];

    public static readonly string[] Description =
    [
        "beskrivning",
        "description",
        "text",
        "merchant",
        "name",
        "referens",
        "reference"
    ];

    public static readonly string[] Amount = ["belopp", "amount", "value"];
}
