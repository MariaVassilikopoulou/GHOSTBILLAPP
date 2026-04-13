import { startTransition, useRef, useState } from "react";
import {
  AnalysisResult,
  ApiError,
  analyzeTransactions,
} from "./services/api";

export type UploadStatus = "idle" | "ready" | "uploading" | "complete" | "error";

export type FilePreview = {
  name: string;
  size: number;
  detectedFormat: string;
  estimatedRows?: number;
};

export type FileState = {
  selectedFile: File | null;
  preview: FilePreview | null;
  uploadStatus: UploadStatus;
};

export type AnalysisState = {
  loading: boolean;
  result: AnalysisResult | null;
  error: ApiError | null;
};

export type RequestState = {
  requestId: number;
  controller: AbortController | null;
};

const SUPPORTED_EXTENSIONS = new Set(["csv", "xlsx", "json", "pdf"]);
const SUPPORTED_MIME_TYPES = new Set([
  "text/csv",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "application/json",
  "application/pdf",
]);

const DEV_RESULT: AnalysisResult = {
  totalTransactionsAnalyzed: 6,
  summary: {
    ghostCount: 1,
    regularCount: 1,
    totalRecurringCharges: 2,
    totalGhostSpend: 47.97,
    totalRegularSpend: 246.15,
  },
  ghosts: [
    {
      merchant: "Netflix",
      classification: "ghost",
      occurrences: 3,
      averageAmount: 15.99,
      totalAmount: 47.97,
      cadenceDays: 29,
      firstChargeDate: "2026-01-05T00:00:00Z",
      lastChargeDate: "2026-03-05T00:00:00Z",
      trend: [
        { label: "2026-01", amount: 15.99 },
        { label: "2026-02", amount: 15.99 },
        { label: "2026-03", amount: 15.99 },
      ],
      transactions: [
        { date: "2026-01-05T00:00:00Z", description: "Netflix", amount: -15.99 },
        { date: "2026-02-05T00:00:00Z", description: "Netflix", amount: -15.99 },
        { date: "2026-03-05T00:00:00Z", description: "Netflix", amount: -15.99 },
      ],
    },
  ],
  regulars: [
    {
      merchant: "City Utilities",
      classification: "regular",
      occurrences: 3,
      averageAmount: 82.05,
      totalAmount: 246.15,
      cadenceDays: 31,
      firstChargeDate: "2026-01-10T00:00:00Z",
      lastChargeDate: "2026-03-10T00:00:00Z",
      trend: [
        { label: "2026-01", amount: 82.15 },
        { label: "2026-02", amount: 84.2 },
        { label: "2026-03", amount: 79.8 },
      ],
      transactions: [
        { date: "2026-01-10T00:00:00Z", description: "City Utilities", amount: -82.15 },
        { date: "2026-02-10T00:00:00Z", description: "City Utilities", amount: -84.2 },
        { date: "2026-03-10T00:00:00Z", description: "City Utilities", amount: -79.8 },
      ],
    },
  ],
};

function getExtension(fileName: string) {
  const extension = fileName.split(".").pop()?.toLowerCase() ?? "";
  return extension;
}

function formatLabel(extension: string) {
  return extension ? extension.toUpperCase() : "Unknown";
}

function validateFile(file: File) {
  const extension = getExtension(file.name);
  const mimeMatches = !file.type || SUPPORTED_MIME_TYPES.has(file.type);
  const extensionMatches = SUPPORTED_EXTENSIONS.has(extension);

  if (!extensionMatches || !mimeMatches) {
    throw new ApiError(
      "Unsupported format. Upload CSV, XLSX, JSON, or PDF.",
      "UNSUPPORTED_FORMAT",
    );
  }
}

async function estimateRows(file: File, extension: string) {
  if (extension === "csv") {
    const text = await file.text();
    const lines = text.split(/\r?\n/).filter((line) => line.trim().length > 0);
    return Math.max(lines.length - 1, 0);
  }

  if (extension === "json") {
    const text = await file.text();
    const parsed = JSON.parse(text) as unknown;
    if (Array.isArray(parsed)) {
      return parsed.length;
    }

    if (
      parsed &&
      typeof parsed === "object" &&
      "transactions" in parsed &&
      Array.isArray((parsed as { transactions?: unknown }).transactions)
    ) {
      return (parsed as { transactions: unknown[] }).transactions.length;
    }
  }

  return undefined;
}

export function useFileUpload() {
  const [fileState, setFileState] = useState<FileState>({
    selectedFile: null,
    preview: null,
    uploadStatus: "idle",
  });
  const [analysisState, setAnalysisState] = useState<AnalysisState>({
    loading: false,
    result: null,
    error: null,
  });
  const [requestState, setRequestState] = useState<RequestState>({
    requestId: 0,
    controller: null,
  });
  const requestIdRef = useRef(0);
  const isDevMode = new URLSearchParams(window.location.search).get("dev") === "1";

  async function selectFile(file: File) {
    try {
      validateFile(file);
      const extension = getExtension(file.name);
      const preview: FilePreview = {
        name: file.name,
        size: file.size,
        detectedFormat: formatLabel(extension),
        estimatedRows: await estimateRows(file, extension),
      };

      startTransition(() => {
        setFileState({
          selectedFile: file,
          preview,
          uploadStatus: "ready",
        });
        setAnalysisState((current) => ({
          ...current,
          error: null,
        }));
      });
    } catch (error) {
      const apiError =
        error instanceof ApiError
          ? error
          : new ApiError("Ghostbill could not read that file.", "INVALID_FILE");

      startTransition(() => {
        setFileState({
          selectedFile: null,
          preview: null,
          uploadStatus: "error",
        });
        setAnalysisState((current) => ({
          ...current,
          error: apiError,
        }));
      });

      throw apiError;
    }
  }

  async function uploadSelectedFile() {
    if (!fileState.selectedFile) {
      return;
    }

    requestState.controller?.abort();
    const controller = new AbortController();
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    setRequestState({ requestId, controller });
    setFileState((current) => ({ ...current, uploadStatus: "uploading" }));
    setAnalysisState((current) => ({ ...current, loading: true, error: null }));

    try {
      const result = isDevMode
        ? await Promise.resolve(DEV_RESULT)
        : await analyzeTransactions(fileState.selectedFile, controller.signal);

      if (requestId !== requestIdRef.current) {
        return;
      }

      startTransition(() => {
        setAnalysisState({
          loading: false,
          result,
          error: null,
        });
        setFileState((current) => ({ ...current, uploadStatus: "complete" }));
      });
    } catch (error) {
      if (controller.signal.aborted || requestId !== requestIdRef.current) {
        return;
      }

      const apiError =
        error instanceof ApiError
          ? error
          : new ApiError("Ghostbill could not analyze that file.", "PARSE_ERROR");

      startTransition(() => {
        setAnalysisState((current) => ({
          ...current,
          loading: false,
          error: apiError,
        }));
        setFileState((current) => ({ ...current, uploadStatus: "error" }));
      });
    }
  }

  function resetFile() {
    requestState.controller?.abort();
    setRequestState((current) => ({ ...current, controller: null }));
    setFileState({
      selectedFile: null,
      preview: null,
      uploadStatus: "idle",
    });
    setAnalysisState((current) => ({
      ...current,
      loading: false,
      error: null,
    }));
  }

  function resetAll() {
    requestState.controller?.abort();
    setRequestState({ requestId: requestIdRef.current, controller: null });
    setFileState({
      selectedFile: null,
      preview: null,
      uploadStatus: "idle",
    });
    setAnalysisState({
      loading: false,
      result: null,
      error: null,
    });
  }

  function clearError() {
    setAnalysisState((current) => ({ ...current, error: null }));
  }

  return {
    fileState,
    analysisState,
    requestState,
    isDevMode,
    selectFile,
    uploadSelectedFile,
    resetFile,
    resetAll,
    clearError,
  };
}
