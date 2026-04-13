import { ChangeEvent, DragEvent, memo, useEffect, useDeferredValue, useRef, useState } from "react";
import "./App.css";
import { MerchantRowModel, useAnalysisMemo } from "./useAnalysisMemo";
import { useFileUpload } from "./useFileUpload";
import { useDismissed } from "./useDismissed";
import { usePrevGhosts } from "./usePrevGhosts";

function GhostIcon({ size = 20, className }: { size?: number; className?: string }) {
  return (
    <svg
      width={size}
      height={Math.round(size * 1.25)}
      viewBox="0 0 20 25"
      fill="currentColor"
      aria-hidden="true"
      className={className}
    >
      <path d="M10 1C5.03 1 1 5.03 1 10v13l3-2.4 3 2.4 3-2.4 3 2.4 3-2.4 3 2.4V10C19 5.03 14.97 1 10 1z" />
      <ellipse cx="7" cy="10.5" rx="1.6" ry="1.8" fill="white" />
      <ellipse cx="13" cy="10.5" rx="1.6" ry="1.8" fill="white" />
    </svg>
  );
}

const currencyFormatter = new Intl.NumberFormat("sv-SE", {
  style: "currency",
  currency: "SEK",
});

const dateFormatter = new Intl.DateTimeFormat("sv-SE", {
  month: "short",
  day: "numeric",
});

function formatBytes(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}


function exportGhostsToCSV(ghosts: MerchantRowModel[]) {
  const header = "Merchant,Per charge (SEK),Times charged,Already paid (SEK),Est. annual (SEK)";
  const rows = ghosts.map((g) =>
    [g.merchant, g.averageAmount.toFixed(2), g.occurrences, g.totalAmount.toFixed(2), g.annualCost.toFixed(2)].join(",")
  );
  const csv = [header, ...rows].join("\n");
  const a = document.createElement("a");
  a.href = "data:text/csv;charset=utf-8," + encodeURIComponent(csv);
  a.download = "ghostbill-ghosts.csv";
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}

const MerchantRow = memo(function MerchantRow({
  merchant,
  onDismiss,
}: {
  merchant: MerchantRowModel;
  onDismiss?: () => void;
}) {
  return (
    <article
      className={`merchant-row merchant-row--${merchant.classification}`}
      style={{ animationDelay: `${merchant.rank * 70}ms` }}
    >
      <div className="merchant-copy">
        <div className="merchant-badges">
          <span className="merchant-badge">
            {merchant.classification === "ghost" ? <><GhostIcon size={12} /> Ghost</> : "Regular"}
          </span>
          {merchant.isNewThisUpload && (
            <span className="new-scan-badge">New this scan</span>
          )}
          {merchant.isNewGhost && !merchant.isNewThisUpload && (
            <span className="new-ghost-badge">New · may be a trial</span>
          )}
        </div>
        <h3>{merchant.merchant}</h3>
        <p>
          Every ~{merchant.cadenceDays} days · {merchant.occurrences} charges · ~{merchant.monthsRunning} months
        </p>
      </div>
      <div className="merchant-metrics">
        <span className="merchant-period-label">already paid</span>
        <strong>{currencyFormatter.format(merchant.totalAmount)}</strong>
        <span>{currencyFormatter.format(merchant.averageAmount)} per charge</span>
        {merchant.classification === "ghost" && (
          <span className="merchant-annual">~{currencyFormatter.format(merchant.annualCost)}/yr</span>
        )}
      </div>
      {onDismiss && (
        <button type="button" className="dismiss-button" onClick={onDismiss}>
          I know about this
        </button>
      )}
    </article>
  );
});

export default function App() {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const {
    fileState,
    analysisState,
    isDevMode,
    selectFile,
    uploadSelectedFile,
    resetFile,
    resetAll,
    clearError,
  } = useFileUpload();
  const { dismissed, dismiss, undismiss } = useDismissed();
  const { prevNames, saveCurrentGhosts } = usePrevGhosts();

  const { ghostMerchants, regularMerchants, monthlyGhostSpend, totalRecurringSpend, annualGhostCost } =
    useAnalysisMemo(analysisState.result, prevNames);
  const deferredGhostMerchants = useDeferredValue(ghostMerchants);
  const deferredRegularMerchants = useDeferredValue(regularMerchants);

  useEffect(() => {
    if (analysisState.result) {
      saveCurrentGhosts(analysisState.result.ghosts.map((g) => g.merchant));
    }
  }, [analysisState.result]);

  const activeGhosts = deferredGhostMerchants.filter((m) => !dismissed.has(m.merchant));
  const dismissedGhosts = deferredGhostMerchants.filter((m) => dismissed.has(m.merchant));

  async function handleFiles(fileList: FileList | null) {
    const file = fileList?.[0];
    if (!file) {
      return;
    }

    try {
      await selectFile(file);
    } catch {}
  }

  async function onInputChange(event: ChangeEvent<HTMLInputElement>) {
    await handleFiles(event.target.files);
    event.target.value = "";
  }

  async function onDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    await handleFiles(event.dataTransfer.files);
  }

  return (
    <div className="app-shell">
      <div className="background-orb background-orb--left" />
      <div className="background-orb background-orb--right" />
      <main className="app">
        <header className="hero">
          <p className="eyebrow">Ghostbill</p>
          <h1>Spot the charges that quietly keep billing you.</h1>
          <p className="hero-copy">
            Upload a bank file to separate likely forgotten
            subscriptions from the bills that keep coming that you actually expect.
          </p>
          <div className="hero-chips">
            <span>CSV</span>
            <span>XLSX</span>
            <span>JSON</span>
            <span>PDF</span>
          </div>
        </header>

        <section className="panel upload-panel">
          <div
            className={[
              "dropzone",
              isDragging ? "dropzone--active" : "",
              fileState.uploadStatus === "idle" ? "dropzone--pulse" : "",
            ]
              .filter(Boolean)
              .join(" ")}
            onDragEnter={(event) => {
              event.preventDefault();
              setIsDragging(true);
            }}
            onDragLeave={(event) => {
              event.preventDefault();
              if (event.currentTarget.contains(event.relatedTarget as Node | null)) {
                return;
              }

              setIsDragging(false);
            }}
            onDragOver={(event) => event.preventDefault()}
            onDrop={onDrop}
          >
            <input
              ref={inputRef}
              className="visually-hidden"
              type="file"
              accept=".csv,.xlsx,.json,.pdf,application/json,application/pdf,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              onChange={onInputChange}
            />
            <div className="dropzone-copy">
              <h2>Drop your bank file here</h2>
              <p>CSV, XLSX, JSON, and PDFs with selectable text work best.</p>
            </div>
            <div className="dropzone-actions">
              <button type="button" className="button button--primary" onClick={() => inputRef.current?.click()}>
                Choose file
              </button>
              <button
                type="button"
                className="button button--secondary"
                onClick={() => void uploadSelectedFile()}
                disabled={!fileState.selectedFile || analysisState.loading}
              >
                {analysisState.loading ? "Analyzing..." : "Analyze"}
              </button>
              <button type="button" className="button button--ghost" onClick={resetFile}>
                Reset file
              </button>
              <button type="button" className="button button--ghost" onClick={resetAll}>
                Reset all
              </button>
            </div>
            {fileState.preview ? (
              <div className="file-preview" aria-live="polite">
                <div>
                  <span className="preview-label">Selected</span>
                  <strong>{fileState.preview.name}</strong>
                </div>
                <div>
                  <span className="preview-label">Size</span>
                  <strong>{formatBytes(fileState.preview.size)}</strong>
                </div>
                <div>
                  <span className="preview-label">Format</span>
                  <strong>{fileState.preview.detectedFormat}</strong>
                </div>
                <div>
                  <span className="preview-label">Estimated rows</span>
                  <strong>
                    {typeof fileState.preview.estimatedRows === "number"
                      ? fileState.preview.estimatedRows
                      : "—"}
                  </strong>
                </div>
              </div>
            ) : (
              <div className="empty-hint">
                <GhostIcon size={48} className="empty-hint__glyph" />
                <p>No ghosts found yet — upload a bank file to scan for forgotten charges</p>
              </div>
            )}
          </div>
        </section>

        {analysisState.error ? (
          <div className="toast" role="status" aria-live="assertive">
            <div>
              <strong>{analysisState.error.code}</strong>
              <p>{analysisState.error.message}</p>
            </div>
            <button type="button" className="button button--ghost" onClick={clearError}>
              Dismiss
            </button>
          </div>
        ) : null}

        <section className="panel summary-panel" aria-live="polite">
          {analysisState.loading ? (
            <div className="skeleton-grid">
              <div className="skeleton-card" />
              <div className="skeleton-card" />
              <div className="skeleton-card" />
            </div>
          ) : analysisState.result ? (
            <div className="stats-grid">
              <article className="stat-card">
                <span>Likely forgotten</span>
                <strong>{currencyFormatter.format(monthlyGhostSpend)}</strong>
                <p>Estimated monthly ghost cost</p>
                {annualGhostCost > 0 && (
                  <span className="stat-annual">~{currencyFormatter.format(annualGhostCost)}/year</span>
                )}
              </article>
              <article className="stat-card">
                <span>Found in your file</span>
                <strong>{currencyFormatter.format(totalRecurringSpend)}</strong>
                <p>Total across all repeat charges in this file</p>
              </article>
              <article className="stat-card">
                <span>Transactions checked</span>
                <strong>{analysisState.result.totalTransactionsAnalyzed}</strong>
                <p>{analysisState.result.summary.totalRecurringCharges} repeat patterns identified</p>
              </article>
            </div>
          ) : (
            <div className="summary-placeholder">
              <h2>Quiet charges show up here.</h2>
              <p>
                Ghostbill does not track income or budgets. It only surfaces charges that keep coming back.
              </p>
            </div>
          )}
        </section>

        <section className="panel merchants-panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Repeat charges</p>
              <h2>Start with likely forgotten charges.</h2>
            </div>
            {isDevMode ? <span className="dev-pill">Dev mode</span> : null}
          </div>

          {analysisState.loading ? (
            <div className="merchant-skeletons">
              {Array.from({ length: 4 }, (_, index) => (
                <div className="merchant-skeleton" key={index} />
              ))}
            </div>
          ) : deferredGhostMerchants.length > 0 || deferredRegularMerchants.length > 0 ? (
            <div className="merchant-groups">
              {activeGhosts.length > 0 && (
                <div className="ghost-callout">
                  <GhostIcon size={28} className="ghost-callout__icon" />
                  <div>
                    <strong>
                      {activeGhosts.length} forgotten{" "}
                      {activeGhosts.length === 1 ? "charge" : "charges"}
                    </strong>
                    <span> quietly costing you ~{currencyFormatter.format(annualGhostCost)} per year</span>
                  </div>
                </div>
              )}
              {deferredGhostMerchants.length > 0 ? (
                <section className="merchant-section merchant-section--ghost">
                  <div className="merchant-section__header">
                    <div>
                      <p className="eyebrow"><GhostIcon size={14} /> Ghosts</p>
                      <h3>Charges you may have forgotten about</h3>
                      <p className="section-intro">Same amount. Same timing. Do you still need these?</p>
                    </div>
                    <div className="merchant-section__actions">
                      <button
                        type="button"
                        className="button button--ghost export-button"
                        onClick={() => exportGhostsToCSV(activeGhosts)}
                        disabled={activeGhosts.length === 0}
                      >
                        Download list
                      </button>
                      <span className="merchant-section__count">{deferredGhostMerchants.length}</span>
                    </div>
                  </div>
                  <div className="merchant-list">
                    {activeGhosts.map((merchant) => (
                      <MerchantRow
                        key={`${merchant.classification}-${merchant.merchant}`}
                        merchant={merchant}
                        onDismiss={() => dismiss(merchant.merchant)}
                      />
                    ))}
                  </div>
                  {dismissedGhosts.length > 0 && (
                    <div className="known-charges">
                      <span>{dismissedGhosts.length} marked as known</span>
                      <span className="known-charges__list">
                        {dismissedGhosts.map((m) => (
                          <button
                            key={m.merchant}
                            type="button"
                            className="dismiss-button"
                            onClick={() => undismiss(m.merchant)}
                          >
                            {m.merchant} · Undo
                          </button>
                        ))}
                      </span>
                    </div>
                  )}
                </section>
              ) : null}
              {deferredRegularMerchants.length > 0 ? (
                <section className="merchant-section">
                  <div className="merchant-section__header">
                    <div>
                      <p className="eyebrow">Regulars</p>
                      <h3>Expected bills that keep coming</h3>
                      <p className="section-intro">These look intentional — bills you likely recognise.</p>
                    </div>
                    <span className="merchant-section__count">{deferredRegularMerchants.length}</span>
                  </div>
                  <div className="merchant-list">
                    {deferredRegularMerchants.map((merchant) => (
                      <MerchantRow key={`${merchant.classification}-${merchant.merchant}`} merchant={merchant} />
                    ))}
                  </div>
                </section>
              ) : null}
            </div>
          ) : (
            <div className="result-empty">
              <p>Upload a supported file to reveal recurring spend patterns.</p>
            </div>
          )}
        </section>

        {analysisState.result ? (
          <section className="panel timeline-panel">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Latest activity</p>
                <h2>Recent recurring charges</h2>
              </div>
            </div>
            <div className="transaction-grid">
              {[...analysisState.result.ghosts, ...analysisState.result.regulars]
                .flatMap((group) => group.transactions.map((transaction) => ({ transaction, group })))
                .sort(
                  (left, right) =>
                    new Date(right.transaction.date).getTime() -
                    new Date(left.transaction.date).getTime(),
                )
                .slice(0, 8)
                .map(({ transaction, group }) => (
                  <article className="transaction-card" key={`${group.merchant}-${transaction.date}`}>
                    <div>
                      <span>{group.merchant}</span>
                      <strong>{currencyFormatter.format(Math.abs(transaction.amount))}</strong>
                    </div>
                    <p>{dateFormatter.format(new Date(transaction.date))}</p>
                  </article>
                ))}
            </div>
          </section>
        ) : null}
      </main>
    </div>
  );
}
