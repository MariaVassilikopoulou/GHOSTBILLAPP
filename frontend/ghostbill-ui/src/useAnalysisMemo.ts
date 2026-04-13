import { useMemo } from "react";
import { AnalysisResult, RecurringExpenseGroup } from "./services/api";

export type MerchantRowModel = RecurringExpenseGroup & {
  rank: number;
  spendShare: number;
  annualCost: number;
  monthsRunning: number;
  priceDrift: number;       // % change first→last charge, 0 if fewer than 2 trend points
  isNewGhost: boolean;      // ghost whose first charge is within the last 60 days
  isNewThisUpload: boolean; // ghost that did not appear in the previous upload's ghost list
};

export function useAnalysisMemo(result: AnalysisResult | null, prevGhostNames: Set<string>) {
  return useMemo(() => {
    if (!result) {
      return {
        ghostMerchants: [] as MerchantRowModel[],
        regularMerchants: [] as MerchantRowModel[],
        maxSpend: 0,
        monthlyGhostSpend: 0,
        totalRecurringSpend: 0,
        annualGhostCost: 0,
      };
    }

    const merchants = [...result.ghosts, ...result.regulars]
      .sort((left, right) => right.totalAmount - left.totalAmount)
      .map((group, index, groups) => ({
        ...group,
        rank: index + 1,
        spendShare: groups[0] ? group.totalAmount / groups[0].totalAmount : 0,
        annualCost: Math.round(group.averageAmount * (365 / group.cadenceDays)),
        monthsRunning: Math.max(1, Math.round((group.occurrences * group.cadenceDays) / 30)),
        priceDrift: group.trend.length >= 2
          ? Math.round(((group.trend[group.trend.length - 1].amount - group.trend[0].amount) / group.trend[0].amount) * 100)
          : 0,
        isNewGhost: group.classification === "ghost" && group.trend.length > 0
          ? (Date.now() - new Date(group.trend[0].date).getTime()) / (1000 * 60 * 60 * 24) <= 60
          : false,
        isNewThisUpload: group.classification === "ghost"
          && prevGhostNames.size > 0
          && !prevGhostNames.has(group.merchant),
      }));

    const ghostMerchants = merchants.filter((merchant) => merchant.classification === "ghost");
    const regularMerchants = merchants.filter((merchant) => merchant.classification === "regular");

    return {
      ghostMerchants,
      regularMerchants,
      maxSpend: merchants[0]?.totalAmount ?? 0,
      monthlyGhostSpend: result.ghosts.reduce(
        (sum, ghost) => sum + ghost.averageAmount * (30 / ghost.cadenceDays),
        0,
      ),
      totalRecurringSpend: merchants.reduce((sum, merchant) => sum + merchant.totalAmount, 0),
      annualGhostCost: result.ghosts.reduce(
        (sum, ghost) => sum + Math.round(ghost.averageAmount * (365 / ghost.cadenceDays)),
        0,
      ),
    };
  }, [result, prevGhostNames]);
}
