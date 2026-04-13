using System.Globalization;
using System.Text.RegularExpressions;

namespace Ghostbill.Api.Parsing.Shared;

public sealed partial class ValueParsingService
{
    private static readonly CultureInfo[] DateCultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("sv-SE"),
        CultureInfo.GetCultureInfo("en-US")
    ];

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "dd-MM-yyyy",
        "MM-dd-yyyy",
        "yyyyMMdd",
        "dd MMM yyyy",
        "MMM dd yyyy",
        "MMM d yyyy",
        "d MMM yyyy"
    ];

    public DateTime ParseDate(string rawValue)
    {
        var value = rawValue.Trim();

        foreach (var culture in DateCultures)
        {
            if (DateTime.TryParseExact(value, DateFormats, culture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed.Date;
            }

            if (DateTime.TryParse(value, culture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.Date;
            }
        }

        throw new FormatException($"Unable to parse date value '{rawValue}'.");
    }

    public decimal ParseAmount(string rawValue)
    {
        var value = rawValue.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Amount is required.");
        }

        var negative = value.Contains('(') && value.Contains(')');
        value = CurrencyCleanupRegex().Replace(value, string.Empty);
        value = value.Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (value.Count(c => c == ',') == 1 && value.Count(c => c == '.') == 0)
        {
            value = value.Replace(',', '.');
        }
        else if (value.Count(c => c == ',') > 1 && value.Contains('.'))
        {
            value = value.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (value.Count(c => c == '.') > 1 && value.Contains(','))
        {
            value = value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else if (value.Count(c => c == ',') == 1 && value.Count(c => c == '.') == 1 && value.IndexOf(',') < value.IndexOf('.'))
        {
            value = value.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Unable to parse amount value '{rawValue}'.");
        }

        return negative ? -Math.Abs(parsed) : parsed;
    }

    [GeneratedRegex(@"[^\d,\.\-\(\)]", RegexOptions.Compiled)]
    private static partial Regex CurrencyCleanupRegex();
}
