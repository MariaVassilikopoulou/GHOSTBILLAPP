namespace Ghostbill.Api.Models;

public sealed class AnalysisResult
{
    public int TotalTransactionsAnalyzed { get; init; }

    public AnalysisSummary Summary { get; init; } = new();

    public IReadOnlyList<RecurringExpenseGroup> Ghosts { get; init; } = [];

    public IReadOnlyList<RecurringExpenseGroup> Regulars { get; init; } = [];
}

public sealed class AnalysisSummary
{
    public int GhostCount { get; init; }

    public int RegularCount { get; init; }

    public int TotalRecurringCharges { get; init; }

    public decimal TotalGhostSpend { get; init; }

    public decimal TotalRegularSpend { get; init; }
}

public sealed class RecurringExpenseGroup
{
    public string Merchant { get; init; } = string.Empty;

    public string Classification { get; init; } = string.Empty;

    public int Occurrences { get; init; }

    public decimal AverageAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public int CadenceDays { get; init; }

    public DateTime FirstChargeDate { get; init; }

    public DateTime LastChargeDate { get; init; }

    public IReadOnlyList<TrendPoint> Trend { get; init; } = [];

    public IReadOnlyList<Transaction> Transactions { get; init; } = [];
}

public sealed class TrendPoint
{
    public string Label { get; init; } = string.Empty;

    public decimal Amount { get; init; }
}
