export type Transaction = {
  date: string;
  description: string;
  amount: number;
};

export type TrendPoint = {
  label: string;
  amount: number;
};

export type RecurringExpenseGroup = {
  merchant: string;
  classification: "ghost" | "regular";
  occurrences: number;
  averageAmount: number;
  totalAmount: number;
  cadenceDays: number;
  firstChargeDate: string;
  lastChargeDate: string;
  trend: TrendPoint[];
  transactions: Transaction[];
};

export type AnalysisSummary = {
  ghostCount: number;
  regularCount: number;
  totalRecurringCharges: number;
  totalGhostSpend: number;
  totalRegularSpend: number;
};

export type AnalysisResult = {
  totalTransactionsAnalyzed: number;
  summary: AnalysisSummary;
  ghosts: RecurringExpenseGroup[];
  regulars: RecurringExpenseGroup[];
};

export type ApiErrorCode =
  | "INVALID_FILE"
  | "UNSUPPORTED_FORMAT"
  | "PARSE_ERROR"
  | "NO_DATA_FOUND";

export class ApiError extends Error {
  code: ApiErrorCode;
  details?: string;

  constructor(message: string, code: ApiErrorCode, details?: string) {
    super(message);
    this.name = "ApiError";
    this.code = code;
    this.details = details;
  }
}

type ErrorResponse = {
  message: string;
  code: ApiErrorCode;
  details?: string;
};

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, "") ?? "";

export async function analyzeTransactions(
  file: File,
  signal?: AbortSignal,
): Promise<AnalysisResult> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${apiBaseUrl}/api/transactions/analyze`, {
    method: "POST",
    body: formData,
    signal,
  });

  if (!response.ok) {
    let error: ErrorResponse | null = null;

    try {
      error = (await response.json()) as ErrorResponse;
    } catch {
      error = null;
    }

    throw new ApiError(
      error?.message ??
        (response.status === 404
          ? "Ghostbill could not reach the analysis API. Start the backend on http://localhost:5270 or set VITE_API_BASE_URL."
          : "Ghostbill could not analyze that file."),
      error?.code ?? "PARSE_ERROR",
      error?.details,
    );
  }

  return (await response.json()) as AnalysisResult;
}
