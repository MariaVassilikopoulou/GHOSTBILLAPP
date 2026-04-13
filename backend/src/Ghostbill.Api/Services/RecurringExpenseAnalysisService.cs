using System.Text.RegularExpressions;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Services;

public interface IRecurringExpenseAnalysisService
{
    AnalysisResult Analyze(IReadOnlyList<Transaction> transactions);
}

public sealed partial class RecurringExpenseAnalysisService : IRecurringExpenseAnalysisService
{
    public AnalysisResult Analyze(IReadOnlyList<Transaction> transactions)
    {
        var expenses = transactions
            .Where(transaction => transaction.Amount < 0)
            .OrderBy(transaction => transaction.Date)
            .ToArray();

        var groups = expenses
            .GroupBy(transaction => NormalizeMerchant(transaction.Description))
            .Where(group => group.Count() >= 2)
            .Select(CreateGroup)
            .Where(group => group is not null)
            .Cast<RecurringExpenseGroup>()
            .OrderByDescending(group => group.TotalAmount)
            .ThenBy(group => group.Merchant, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ghosts = groups.Where(group => group.Classification == "ghost").ToArray();
        var regulars = groups.Where(group => group.Classification == "regular").ToArray();

        return new AnalysisResult
        {
            TotalTransactionsAnalyzed = expenses.Length,
            Ghosts = ghosts,
            Regulars = regulars,
            Summary = new AnalysisSummary
            {
                GhostCount = ghosts.Length,
                RegularCount = regulars.Length,
                TotalRecurringCharges = groups.Length,
                TotalGhostSpend = ghosts.Sum(group => group.TotalAmount),
                TotalRegularSpend = regulars.Sum(group => group.TotalAmount)
            }
        };
    }

    private static RecurringExpenseGroup? CreateGroup(IGrouping<string, Transaction> group)
    {
        var ordered = group.OrderBy(transaction => transaction.Date).ToArray();
        var intervals = ordered.Zip(ordered.Skip(1), (left, right) => (right.Date - left.Date).Days).ToArray();

        if (intervals.Length == 0)
        {
            return null;
        }

        var averageInterval = (int)Math.Round(intervals.Average(), MidpointRounding.AwayFromZero);
        if (averageInterval is < 7 or > 40)
        {
            return null;
        }

        var amounts = ordered.Select(transaction => Math.Abs(transaction.Amount)).ToArray();
        var averageAmount = amounts.Average();
        var amountVariance = averageAmount == 0 ? 0 : (amounts.Max() - amounts.Min()) / averageAmount;
        var intervalVariance = intervals.Max() - intervals.Min();

        var classification = ordered.Length >= 3 && amountVariance <= 0.03m && intervalVariance <= 5
            ? "ghost"
            : amountVariance <= 0.35m && intervalVariance <= 12
                ? "regular"
                : null;

        if (classification is null)
        {
            return null;
        }

        return new RecurringExpenseGroup
        {
            Merchant = ordered
                .GroupBy(transaction => transaction.Description.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(descriptionGroup => descriptionGroup.Count())
                .ThenBy(descriptionGroup => descriptionGroup.Key, StringComparer.OrdinalIgnoreCase)
                .First()
                .Key,
            Classification = classification,
            Occurrences = ordered.Length,
            AverageAmount = decimal.Round(amounts.Average(), 2, MidpointRounding.AwayFromZero),
            TotalAmount = decimal.Round(amounts.Sum(), 2, MidpointRounding.AwayFromZero),
            CadenceDays = averageInterval,
            FirstChargeDate = ordered.First().Date,
            LastChargeDate = ordered.Last().Date,
            Trend = ordered
                .GroupBy(transaction => new { transaction.Date.Year, transaction.Date.Month })
                .OrderBy(monthGroup => monthGroup.Key.Year)
                .ThenBy(monthGroup => monthGroup.Key.Month)
                .Select(monthGroup => new TrendPoint
                {
                    Label = $"{monthGroup.Key.Year}-{monthGroup.Key.Month:00}",
                    Amount = decimal.Round(monthGroup.Sum(transaction => Math.Abs(transaction.Amount)), 2, MidpointRounding.AwayFromZero)
                })
                .ToArray(),
            Transactions = ordered
        };
    }

    private static string NormalizeMerchant(string description)
    {
        var normalized = MerchantNoiseRegex().Replace(description.ToUpperInvariant(), " ");
        normalized = CollapseWhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized;
    }

    [GeneratedRegex(@"[\d\p{P}\p{S}]+", RegexOptions.Compiled)]
    private static partial Regex MerchantNoiseRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex CollapseWhitespaceRegex();
}
