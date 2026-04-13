# Fullstack Implementation Prompt — Ghostbill

## Role
You are a fullstack developer implementing the complete Ghostbill application. This prompt is self-contained and authoritative — follow it entirely. It supersedes the specialized backend and frontend prompts where they conflict.

Your task: build or extend Ghostbill so it accepts transaction files in four formats, analyzes recurring outgoing expenses, and presents the results in a polished React UI.

---

## Implementation Priorities
Correctness > compatibility > determinism > performance > polish
Determinism > creativity
Simplicity > abstraction

---

## Product Context

### What Ghostbill Does
Ghostbill is an expense-awareness tool that helps users identify recurring outgoing charges they may have forgotten about — especially subscriptions and repeat expenses that quietly drain money over time.

The user uploads a transaction export or bank statement (`.csv`, `.xlsx`, `.json`, or text-based `.pdf`). Ghostbill analyzes the transactions and surfaces:
- **Ghosts** — consistent timing and amount; likely a forgotten subscription
- **Regulars** — expected variation; bills the user probably recognises

### Core Product Rules
- Focus on recurring outgoing expenses only — not income, savings, or general finance
- Credits, income, refunds, and positive cash-flow entries are excluded from all analysis
- "Ghost" = likely forgotten or overlooked, not fraudulent
- The recurring pattern matters — isolated one-off transactions are not surfaced
- Equivalent transaction data must produce equivalent analysis results regardless of format

### What the User Sees
- A merchant list grouped into Ghost / Regular sections
- A timeline panel with the 8 most recent individual recurring charges
- A "New · may be a trial" badge on ghost merchants whose first charge was within the last 60 days
- Clear feedback for unsupported or unparseable files

### Hero Section (Exact Text Required)
- **Headline:** "Spot the charges that quietly keep billing you."
- **Subtitle:** "Upload a bank file to separate likely forgotten subscriptions from the bills that keep coming that you actually expect."
- **Format chips:** CSV · XLSX · JSON · PDF as visual pill badges

---

## Global Constraints — Read Before Anything Else

### Must Not Change
- `CsvParsingService` internals, signature, or execution order
- Existing API route, request field names, parameter list, or DTO shapes
- Existing pipeline order or semantics
- Observable CSV output (ordering, filtering, normalization, validation, error surface)
- Existing business logic behavior

### Blocked Actions
- Any change to `CsvParsingService`
- Any business logic inside parser implementations
- Any change to pipeline order or API contract
- Any OCR-based PDF implementation
- Any non-deterministic or time-dependent logic
- Any new frontend dependencies or UI frameworks

---

## Glossary

| Term | Definition |
|------|-----------|
| Parse | Convert a file into `List<Transaction>` |
| Extract | Pull raw rows from a document (PDF step before parsing) |
| Ghost | Recurring charge classified as likely forgotten |
| Regular | Recurring charge classified as expected/intentional |
| Observable behavior | What an end user or test sees |
| Deterministic | Same input → identical output on every run |
| cadenceDays | Average interval in days between consecutive charges for a merchant |

---

## Shared Contracts

### Processing Pipeline (Non-Negotiable)

```
File → Parser → List<Transaction> → Analysis → AnalysisResult
```

- Parsers are translators only
- Business logic lives exclusively in the analysis layer
- Analysis layer is format-agnostic: no format-specific branching, no parser metadata, analysis must not know which parser produced its input

### Transaction Model (Backend)

```csharp
public class Transaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
```

### AnalysisResult
Use the existing `AnalysisResult` shape. Do not invent a new response model.

Key field: `totalTransactionsAnalyzed` reflects outgoing (negative-amount) transactions only — income and credits are excluded. The number will be lower than the file's total row count.

### TrendPoint (Backend produces, Frontend consumes)

```typescript
type TrendPoint = {
  label: string;   // "YYYY-MM" — one entry per calendar month
  amount: number;  // sum of transaction amounts for that month
}
// Ordered chronologically. Frontend must not reorder or invent data.
```

Backend: populate per `RecurringExpenseGroup` by grouping its transactions by calendar month.
Frontend: use as the data source for sparklines.

### API Contract

```
POST /api/transactions/analyze
Content-Type: multipart/form-data
```

Success: returns `AnalysisResult`

Error response:
```json
{
  "message": "string",
  "code": "INVALID_FILE | UNSUPPORTED_FORMAT | PARSE_ERROR | NO_DATA_FOUND",
  "details": "string (optional)"
}
```

| Code | Condition |
|------|-----------|
| `INVALID_FILE` | File missing, empty, unreadable, or invalid before parsing |
| `UNSUPPORTED_FORMAT` | No parser found for the file extension |
| `PARSE_ERROR` | Parser found but throws, or input shape unsupported for that format |
| `NO_DATA_FOUND` | Preserve existing CSV behavior only |

Rules: no parser exception may leak past the controller; no format-specific error branching; CSV error behavior must not change.

### Currency and Locale
All monetary values assume **SEK**.
Frontend formatter: `Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' })`
Apply to: stat cards, merchant rows, "already paid" totals, annual projections, ghost callout banner, timeline cards.

---

## Supported Input Formats

| Format | Rule |
|--------|------|
| CSV | Existing behavior is the source of truth. Preserve entirely. |
| XLSX | Same pipeline as CSV. Must produce equivalent results for equivalent tabular content. Use **ClosedXML**. |
| JSON | Two accepted root shapes: top-level array, or object with `transactions` array. Reject other shapes with `PARSE_ERROR`. |
| PDF | Text-based, machine-readable only. No OCR. Uses named-strategy pattern (see below). `PARSE_ERROR` if no rows extractable — never return empty list. |

---

## Backend Specification

### Allowed Existing File Modifications
- Controller/orchestration entrypoint — integration wiring only
- `Program.cs` — DI registration only
- `Ghostbill.Api.csproj` — package references only
- Backend test project files — tests only

No other existing files may be modified without explicit approval.

### New Components — Exact Paths and Namespaces

All paths relative to `backend/src/Ghostbill.Api/`. All shared helpers under `Ghostbill.Api.Parsing.Shared` only.

**Abstractions and Resolution:**

| # | Path | Namespace |
|---|------|-----------|
| 1 | `Parsing/Abstractions/ITransactionFileParser.cs` | `Ghostbill.Api.Parsing.Abstractions` |
| 2 | `Parsing/Resolution/ParserResolutionService.cs` | `Ghostbill.Api.Parsing.Resolution` |

**Parsers:**

| # | Path | Namespace |
|---|------|-----------|
| 3 | `Parsing/Parsers/CsvFileParserAdapter.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 4 | `Parsing/Parsers/ExcelParsingService.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 5 | `Parsing/Parsers/JsonParsingService.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 6 | `Parsing/Parsers/PdfParsingService.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 7 | `Parsing/Parsers/IPdfTransactionExtractionStrategy.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 8 | `Parsing/Parsers/ColumnLayoutPdfStrategy.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 9 | `Parsing/Parsers/SequentialTablePdfStrategy.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 10 | `Parsing/Parsers/RegexRowPdfStrategy.cs` | `Ghostbill.Api.Parsing.Parsers` |

**Shared Helpers:**

| # | Path | Namespace | Purpose |
|---|------|-----------|---------|
| 11 | `Parsing/Shared/HeaderDetectionService.cs` | `Ghostbill.Api.Parsing.Shared` | Header row detection (scans first 20 rows; needs 2 of 3 field types) |
| 12 | `Parsing/Shared/ColumnMappingService.cs` | `Ghostbill.Api.Parsing.Shared` | Field-to-column mapping |
| 13 | `Parsing/Shared/ValueParsingService.cs` | `Ghostbill.Api.Parsing.Shared` | Date and amount parsing |
| 14 | `Parsing/Shared/RowMaterializationService.cs` | `Ghostbill.Api.Parsing.Shared` | Row → Transaction conversion |
| 15 | `Parsing/Shared/PdfRowFilter.cs` | `Ghostbill.Api.Parsing.Shared` | PDF header/noise filtering (`LooksLikeHeader`, `LooksLikeNoise`) |
| 16 | `Parsing/Shared/HeaderNormalization.cs` | `Ghostbill.Api.Parsing.Shared` | Lowercase + trim + diacritic-strip for alias matching |
| 17 | `Parsing/Shared/ParsingAliases.cs` | `Ghostbill.Api.Parsing.Shared` | Authoritative column header alias lists |

`ParseDiagnostics` is out of scope and must not be introduced.

### Column Header Aliases (`ParsingAliases.cs`)

| Field | Accepted aliases |
|-------|----------------|
| Date | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference` |
| Amount | `belopp`, `amount`, `value` |

Files with headers not on this list will fail to map.

### JSON Property Aliases

| Field | Accepted keys |
|-------|--------------|
| Date | `date`, `transactionDate`, `postedDate` |
| Description | `description`, `text`, `merchant`, `name` |
| Amount | `amount`, `value` |

### Component Responsibilities

**`ITransactionFileParser`:** defines `CanHandle(extension)` and `Parse()`. No parsing logic, no business logic, no resolution.

**`ParserResolutionService`:** resolves parser by `CanHandle(extension)` only — deterministic and order-independent.
- 0 matches → `UNSUPPORTED_FORMAT`
- >1 matches → configuration exception at startup (developer error)
- Must not depend on DI registration order

**`CsvFileParserAdapter`:** pure pass-through to `CsvParsingService`. No transformation of any kind.

**`ExcelParsingService`:** reads first worksheet via ClosedXML. Uses shared helpers only for translation. No business logic.

**`JsonParsingService`:** reads UTF-8/BOM JSON. Supports only the two root shapes above. Unsupported shapes → `PARSE_ERROR`. No business logic.

**`PdfParsingService` (Orchestrator Only):**
- Runs strategies in fixed precedence: `ColumnLayout` → `SequentialTable` → `RegexRow`
- Document-level "first winner": first strategy with non-empty rows wins for all pages — no per-page switching, no mixing
- Deduplicates rows across pages using a hash key before materialization
- Delegates to `RowMaterializationService`
- Throws `ParsingException("PARSE_ERROR")` if no strategy yields results
- Must not contain extraction logic inline; must not register strategies in DI

**`IPdfTransactionExtractionStrategy`:**
```csharp
public interface IPdfTransactionExtractionStrategy
{
    string Name { get; }
    bool TryExtract(PdfDocument document, out IReadOnlyList<IReadOnlyList<string>> rows);
}
```
Each strategy: iterates pages internally; returns `true` + rows on success; `false` + empty on failure; deterministic; `sealed partial class` (for `[GeneratedRegex]`); not registered in DI.

**`ColumnLayoutPdfStrategy`:** bounding-box word-position extraction. Groups words by Y-coordinate, extracts fields by X-range. Targets column-layout bank statements (Swedbank-style).

**`SequentialTablePdfStrategy`:** regex-based sequential extraction targeting structured table exports with TXN IDs (Debit/Credit type, currency, balance).

**`RegexRowPdfStrategy`:** generic fallback — two regex passes (per-line and whitespace-normalized full-page). Last resort.

**`PdfRowFilter`:** `LooksLikeHeader()` and `LooksLikeNoise()` — used by all strategies before yielding a candidate row.

**Shared helpers:** do not depend on `CsvParsingService`; do not contain analysis logic.

### Analysis Algorithm (Authoritative — Do Not Change)

Implemented in `RecurringExpenseAnalysisService.cs`.

**Scope:** negative-amount transactions only. Positive entries are filtered out before grouping.

**Merchant normalization (grouping key):**
1. Uppercase
2. Strip punctuation, digits, non-letter symbols: `[^A-ZÅÄÖ ]`
3. Collapse whitespace

**Display name:** most frequent raw (un-normalized) variant in the group. If tied, use chronologically earliest.

**Group filters** (silently discard failing groups — no user message):

| Filter | Rule |
|--------|------|
| Minimum occurrences | ≥ 2 |
| Cadence bounds | Average interval **7–40 days** |

**Classification thresholds (exact values):**
```
amountVariance   = (max_amount - min_amount) / mean_amount
intervalVariance = max_interval_days - min_interval_days

Ghost:   occurrences ≥ 3  AND  amountVariance ≤ 0.03  AND  intervalVariance ≤ 5
Regular: amountVariance ≤ 0.35  AND  intervalVariance ≤ 12
Neither: silently discarded
```

**Output sort** (within each classification bucket):
1. `TotalAmount` descending
2. `Merchant` ascending, case-insensitive A–Z (tiebreaker)

### Libraries

| Purpose | Library |
|---------|---------|
| XLSX | **ClosedXML** |
| PDF text extraction | **UglyToad.PdfPig** |
| OCR | **Not allowed** |

### Temp File Cleanup
If a temp file is created, delete it in a `finally` block on all exit paths (success, unsupported format, parse error, no-data). Not required if validation fails before file creation.

---

## Frontend Specification

### Environment
- React 18 (hooks only), TypeScript, Vite, plain CSS — no Tailwind, no UI frameworks
- `/services/api.ts` — **DO NOT MODIFY**

### State Architecture (Three Independent Domains)
```
fileState:     { selectedFile, preview: { name, size, detectedFormat, estimatedRows? }, uploadStatus }
analysisState: { loading, result, error }
requestState:  { requestId: number, controller: AbortController | null }
```

Latest-request-wins: increment `requestId` per upload, abort previous controller, ignore stale responses.

**Memoization:**
- `useMemo` for merchant rows, stat card values, timeline data, `annualGhostCost`
- `React.memo` on the merchant row component
- `useDeferredValue` for sorted merchant array

### MerchantRowModel Computed Fields

| Field | Formula |
|-------|---------|
| `rank` | 1-based position sorted by `totalAmount` desc |
| `spendShare` | `totalAmount / topMerchant.totalAmount` |
| `annualCost` | `Math.round(averageAmount × (365 / cadenceDays))` |
| `monthsRunning` | `Math.max(1, Math.round((occurrences × cadenceDays) / 30))` |
| `priceDrift` | `Math.round(((lastTrend - firstTrend) / firstTrend) × 100)` or `0` if < 2 trend points |
| `isNewGhost` | `classification === "ghost" && daysSince(firstChargeDate) <= 60` |

`useAnalysisMemo` also exposes `annualGhostCost: number` (sum of `annualCost` across ghost merchants).

### File Upload UX
- Drag-and-drop zone with overlay on `dragenter`; hover/active states
- Validation by extension and MIME type: CSV, XLSX, JSON, PDF
- **File preview** (after selection, before upload):
  - File name, file size, detected format label
  - Row count for CSV/JSON only (best-effort); **no row count for PDF or XLSX**
- **Two reset buttons:**

| Button | Clears |
|--------|--------|
| "Reset file" | file, preview, uploadStatus, loading, error. **Keeps** `result` visible. |
| "Reset all" | Everything above plus `result`. Panel disappears. |

### States

**Loading:** skeleton loaders for stat cards and merchant rows — no spinner-only UI.

**Empty state:**
- Text: "No ghosts found yet — upload a statement to scan for forgotten charges"
- Visual: `GhostIcon` at 48px, `color: rgba(207,127,40,0.5)`
- Drag zone pulses gently (CSS only); stops on drag or upload

**Error:** CSS-only toast, `aria-live="assertive"`, dismissible. `UNSUPPORTED_FORMAT` error must name the supported formats.

### Animations

| Effect | Rule |
|--------|------|
| Row stagger | `animationDelay: rank × 70ms` |
| Stats count-up | RAF or CSS |
| Spend bars | CSS gradient |
| Drag zone pulse | CSS only; stops on drag/upload |
| Libraries | Not allowed |

### Ghost Icon Component (`GhostIcon`)
Inline SVG in `App.tsx` — no separate file, no emoji, no external image. Domed head, scalloped wavy base, two white oval eyes. `currentColor` fill. Accepts `size` prop (px). Used at 12px, 14px, 28px, 48px.

### Merchant Row Layout
1. Badge (ghost amber / regular green) + optional "New · may be a trial" badge (when `isNewGhost`)
2. Merchant name and average amount
3. Spend bar — gradient, width = `spendShare × 100%`
4. Sparkline — SVG only, from `group.trend`, lightweight shape (no axis labels)
5. Description: `Every ~{cadenceDays} days · {occurrences} charges · ~{monthsRunning} months`
6. "Already paid": `totalAmount` in SEK + `averageAmount` in SEK "per charge"
7. Annual projection (ghost only): `~{annualCost}/yr` in amber bold

**Badge colors:**
- Ghost: `background: rgba(207,127,40,0.14)`, `color: #7a4200` + `GhostIcon` at 12px inline
- Regular: `background: rgba(30,95,87,0.1)`, `color: #1a5e56`

### Ghost and Regular Sections

**Ghost section:**
- Eyebrow: `<GhostIcon size={14} /> Ghosts`
- Heading: "Charges you may have forgotten about"
- Intro: "Same amount. Same timing. Do you still need these?"
- Left border: `border-left: 3px solid rgba(207,127,40,0.4)`
- **Callout banner** (when ghosts exist): `<GhostIcon size={28} color="#cf7f28" />` + "{N} forgotten charge(s) quietly costing you ~{annualGhostCost} per year"; warm amber gradient background

**Regular section:**
- Heading: "Expected bills that keep coming"
- Intro: "These look intentional — bills you likely recognise."

### Summary Panel — 3 Stat Cards (All Required)

| Card | Primary | Subtext | Extra |
|------|---------|---------|-------|
| "Likely forgotten" | `totalGhostSpend` | "Estimated monthly ghost cost" | `~{annualGhostCost}/year` in amber bold (when > 0) |
| "Found in your file" | `totalGhostSpend + totalRegularSpend` | "Total across all repeat charges in this file" | — |
| "Transactions checked" | `totalTransactionsAnalyzed` | "{totalRecurringCharges} repeat patterns identified" | — |

Skeleton placeholders during loading.

### Timeline Panel — "Recent Recurring Charges"
Render below the merchant list when `result` is present. Do not render when there are no results.

- **Heading:** "Recent recurring charges"
- **Content:** 8 most recent transactions from all groups, sorted by `transaction.date` descending
- **Each card:** merchant name · amount in SEK (absolute value) · date as "MMM D" in user's local timezone
- **Layout:** horizontal scrollable grid

### Dev Mode (`?dev=1`)
Skip API calls, use inline mock `AnalysisResult` with:
- Ghost: "Netflix", ~149 SEK, every ~30 days, 6+ occurrences
- Regular: "City Utilities", variable ~300–450 SEK, monthly

Show a **"Dev" pill badge** in the merchant panel heading.

### Accessibility and Responsiveness
- `aria-live="polite"` for results/loading; `aria-live="assertive"` for errors
- Keyboard-accessible upload; visible focus states
- Mobile: tap-to-upload, single-column; Desktop: drag-and-drop, hover states

---

## Tests Required

### Backend Parity (Mandatory)
CSV, XLSX, and JSON results for semantically equivalent input must be structurally, semantically, and deterministically equivalent. Proven by automated comparison tests.

### PDF Orchestrator Tests (3 Required)

**Precedence:** given a PDF matching both `SequentialTablePdfStrategy` and `RegexRowPdfStrategy`, result must use SequentialTable rows exclusively.

**Failure:** a PDF with no machine-readable text must throw `PARSE_ERROR`. No partial result, no empty list.

**No-mixing:** a multi-page PDF where page 1 matches strategy A and page 2 would only match strategy B must produce strategy A rows for all pages.

---

## Deliverables

### Backend
All 17 new component files. Only modify the explicitly allowed existing files (controller, `Program.cs`, `.csproj`, test project).

### Frontend
Return only these files, fully functional:
1. `frontend/ghostbill-ui/src/App.tsx`
2. `frontend/ghostbill-ui/src/App.css`
3. `frontend/ghostbill-ui/src/useFileUpload.ts`
4. `frontend/ghostbill-ui/src/useAnalysisMemo.ts`
5. `CHANGELOG.md` (max 5 lines)

**Rules for all deliverables:**
- No explanations or markdown outside code files
- No placeholder TODOs
- No partial implementation
- Must compile without TypeScript errors or console warnings
- Backend tests must pass

---

## Acceptance Criteria

### Backend
- [ ] CSV behavior unchanged
- [ ] XLSX parses and produces equivalent results to equivalent CSV
- [ ] JSON parses with correct alias mapping; unsupported shapes return `PARSE_ERROR`
- [ ] Text-based PDFs parse via strategy pattern; result is equivalent where data matches
- [ ] Unsupported file extension → `UNSUPPORTED_FORMAT`
- [ ] Malformed supported file → `PARSE_ERROR`
- [ ] PDF precedence test passes
- [ ] PDF failure test passes
- [ ] PDF no-mixing test passes
- [ ] CSV/XLSX/JSON parity tests pass

### Frontend
- [ ] Hero section: exact headline, subtitle, format chips
- [ ] Drag or select any supported file → file preview shown; PDF/XLSX show no row count
- [ ] Upload → skeleton loaders → animated results in SEK
- [ ] Rows staggered by rank (70ms × rank)
- [ ] Spend bar proportional to top merchant, not combined total
- [ ] Sparkline from `trend` monthly data
- [ ] "Reset file" preserves results; "Reset all" clears everything
- [ ] Multiple uploads produce no stale data
- [ ] 3 stat cards with correct labels, values, and subtext
- [ ] "Already paid" display on every merchant row
- [ ] Annual `~X/yr` projection on ghost rows
- [ ] "New · may be a trial" badge on ghost rows where `isNewGhost`
- [ ] Ghost section: eyebrow, heading, intro, left-border, callout banner
- [ ] Regular section heading: "Expected bills that keep coming"
- [ ] Timeline panel with up to 8 cards
- [ ] Empty state: GhostIcon (faded amber), correct text
- [ ] Error toasts for `INVALID_FILE`, `UNSUPPORTED_FORMAT`, `PARSE_ERROR`
- [ ] Mobile: tap-to-upload, vertical layout
- [ ] Desktop: drag-and-drop, hover states
- [ ] Dev mode: Netflix + City Utilities, "Dev" badge

### End-to-End
- [ ] A supported file uploaded from the frontend is analyzed by the backend and results rendered
- [ ] Analysis output is format-agnostic — equivalent data produces equivalent results across formats
