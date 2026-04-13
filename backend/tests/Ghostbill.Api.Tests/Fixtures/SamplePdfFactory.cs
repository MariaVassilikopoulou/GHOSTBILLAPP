using System.Text;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Tests;

internal static class SamplePdfFactory
{
    public static byte[] CreateStatementPdf(IReadOnlyList<Transaction> transactions)
    {
        var lines = transactions
            .OrderBy(transaction => transaction.Date)
            .Select(transaction => $"{transaction.Date:yyyy-MM-dd} {Escape(transaction.Description)} {transaction.Amount:0.00}")
            .ToArray();

        var content = BuildContentStream(lines);
        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj\n",
            $"4 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}\nendstream endobj\n",
            "5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0 };
        foreach (var pdfObject in objects)
        {
            offsets.Add(stream.Position);
            writer.Write(pdfObject);
            writer.Flush();
        }

        var xrefStart = stream.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write($"{offset:0000000000} 00000 n \n");
        }

        writer.Write($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\n");
        writer.Write($"startxref\n{xrefStart}\n%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string BuildContentStream(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.Append("BT\n/F1 12 Tf\n72 740 Td\n");
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                builder.Append("T*\n");
            }

            builder.Append('(');
            builder.Append(lines[index]
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal));
            builder.Append(") Tj\n");
        }

        builder.Append("ET");
        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\n", " ", StringComparison.Ordinal).Trim();
}
