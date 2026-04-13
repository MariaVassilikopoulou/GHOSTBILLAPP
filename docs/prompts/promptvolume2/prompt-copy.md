# Fullstack Implementation Prompt — Ghostbill (v2)

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

The user uploads a bank file (`.csv`, `.xlsx`, `.json`, or text-based `.pdf`). Ghostbill analyzes the transactions and surfaces:
- **Ghosts** — consistent timing and amount; likely a forgotten subscription
- **Regulars** — expected variation; bills the user probably recognises

### Core Product Rules
- Focus on recurring outgoing expenses only — not income, savings, or general finance
- Credits, income, refunds, and positive cash-flow entries are excluded from all analysis
- "Ghost" = likely forgotten or overlooked, not fraudulent
- The recurring pattern matters — isolated one-off transactions are not surfaced
- Equivalent transaction data must produce equivalent analysis results regardless of format

### What the User Sees
- A ghost section with a callout banner, merchant list, export button, and dismiss controls
- A regular section with a merchant list
- A "New this scan" badge on ghosts that weren't in the previous upload's ghost list
- A "New · may be a trial" badge on ghost merchants whose first charge was within the last 60 days
- A "Known charges" section for dismissed ghosts with individual undo controls
- A timeline panel with the 8 most recent individual recurring charges
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
- Analysis layer is format-agnostic

### Transaction Model (Backend)

```csharp
public class Transaction
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
```

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

### Currency and Locale
All monetary values assume **SEK**.
Frontend formatter: `Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' })`

---

## Supported Input Formats

| Format | Rule |
|--------|------|
| CSV | Existing behavior is the source of truth. Preserve entirely. |
| XLSX | Same pipeline as CSV. Must produce equivalent results for equivalent tabular content. Use **ClosedXML**. |
| JSON | Two accepted root shapes: top-level array, or object with `transactions` array. Reject other shapes with `PARSE_ERROR`. |
| PDF | Text-based, machine-readable only. No OCR. Uses named-strategy pattern. `PARSE_ERROR` if no rows extractable. |

---

## Backend Specification

### Allowed Existing File Modifications
- Controller/orchestration entrypoint — integration wiring only
- `Program.cs` — DI registration only
- `Ghostbill.Api.csproj` — package references only
- Backend test project files — tests only

No other existing files may be modified without explicit approval.

### New Components — Exact Paths and Namespaces

All paths relative to `backend/src/Ghostbill.Api/`.

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
| 11 | `Parsing/Shared/HeaderDetectionService.cs` | `Ghostbill.Api.Parsing.Shared` | Header row detection |
| 12 | `Parsing/Shared/ColumnMappingService.cs` | `Ghostbill.Api.Parsing.Shared` | Field-to-column mapping |
| 13 | `Parsing/Shared/ValueParsingService.cs` | `Ghostbill.Api.Parsing.Shared` | Date and amount parsing |
| 14 | `Parsing/Shared/RowMaterializationService.cs` | `Ghostbill.Api.Parsing.Shared` | Row → Transaction conversion |
| 15 | `Parsing/Shared/PdfRowFilter.cs` | `Ghostbill.Api.Parsing.Shared` | PDF header/noise filtering |
| 16 | `Parsing/Shared/HeaderNormalization.cs` | `Ghostbill.Api.Parsing.Shared` | Header string normalization |
| 17 | `Parsing/Shared/ParsingAliases.cs` | `Ghostbill.Api.Parsing.Shared` | Accepted column header aliases |

### Column Header Aliases

| Field | Accepted aliases |
|-------|----------------|
| Date | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference` |
| Amount | `belopp`, `amount`, `value` |

### JSON Property Aliases

| Field | Accepted keys |
|-------|--------------|
| Date | `date`, `transactionDate`, `postedDate` |
| Description | `description`, `text`, `merchant`, `name` |
| Amount | `amount`, `value` |

### Analysis Algorithm (Authoritative — Do Not Change)

**Scope:** negative-amount transactions only. Positive entries are filtered out before grouping.

**Merchant normalization (grouping key):**
1. Uppercase
2. Strip punctuation, digits, non-letter symbols: `[^A-ZÅÄÖ ]`
3. Collapse whitespace

**Display name:** most frequent raw (un-normalized) variant in the group. If tied, use chronologically earliest.

**Group filters** (silently discard failing groups):

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

### PDF Strategy Pattern
`PdfParsingService` is an orchestrator only:
- Runs strategies in fixed precedence: `ColumnLayout` → `SequentialTable` → `RegexRow`
- Document-level "first winner": first strategy with non-empty rows wins for all pages — no per-page switching, no mixing
- Throws `PARSE_ERROR` if no strategy yields results

```csharp
public interface IPdfTransactionExtractionStrategy
{
    string Name { get; }
    bool TryExtract(PdfDocument document, out IReadOnlyList<IReadOnlyList<string>> rows);
}
```

Each strategy: iterates pages internally; `sealed partial class`; not registered in DI.

### Libraries

| Purpose | Library |
|---------|---------|
| XLSX | **ClosedXML** |
| PDF text extraction | **UglyToad.PdfPig** |
| OCR | **Not allowed** |

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

### localStorage State
| Key | Type | Purpose |
|-----|------|---------|
| `ghostbill_dismissed` | `string[]` | Merchant names the user has marked as known |
| `ghostbill_prev_ghosts` | `string[]` | Ghost merchant names from the previous upload |

Managed by two custom hooks: `useDismissed` and `usePrevGhosts`.

### MerchantRowModel Computed Fields

| Field | Formula |
|-------|---------|
| `rank` | 1-based position sorted by `totalAmount` desc |
| `annualCost` | `Math.round(averageAmount × (365 / cadenceDays))` |
| `monthsRunning` | `Math.max(1, Math.round((occurrences × cadenceDays) / 30))` |
| `isNewGhost` | `classification === "ghost" && daysSince(firstChargeDate) <= 60` |
| `isNewThisUpload` | `classification === "ghost" && prevGhostNames.size > 0 && !prevGhostNames.has(merchant)` |

`useAnalysisMemo` exposes: `annualGhostCost: number` (sum of `annualCost` across ghost merchants).
`useAnalysisMemo` signature: `useAnalysisMemo(result: AnalysisResult | null, prevGhostNames: Set<string>)`.

### File Upload UX
- Drag-and-drop zone; validation by extension and MIME type
- File preview: name, size, format; row count for CSV/JSON only
- Two reset buttons: "Reset file" (keeps result visible) and "Reset all" (clears everything)

### Ghost Icon Component (`GhostIcon`)
Inline SVG in `App.tsx`. Domed head, scalloped wavy base, two white oval eyes. `currentColor` fill. Accepts `size` prop. Used at 12px, 14px, 28px, 48px.

### Merchant Row Layout
Two-column grid (merchant info | spend metrics) with optional dismiss button:

1. Badge (ghost amber / regular green) + optional "New this scan" or "New · may be a trial" badge
2. Merchant name
3. Description: `Every ~{cadenceDays} days · {occurrences} charges · ~{monthsRunning} months`
4. "Already paid": `totalAmount` in SEK + `averageAmount` "per charge" (right-aligned)
5. Annual projection (ghost only): `~{annualCost}/yr` in amber bold
6. "I know about this" dismiss button (ghost rows only)

**Badge logic:**
- `isNewThisUpload`: show "New this scan" badge (green tint)
- `isNewGhost && !isNewThisUpload`: show "New · may be a trial" badge (red tint)

### Ghost Section
- Eyebrow: `<GhostIcon size={14} /> Ghosts`
- Heading: "Charges you may have forgotten about"
- Intro: "Same amount. Same timing. Do you still need these?"
- Left border: `border-left: 3px solid rgba(207,127,40,0.4)`
- **Header right side:** "Download list" button + count badge
- **Callout banner** (when active ghosts > 0): `<GhostIcon size={28} />` + "{N} forgotten charge(s) quietly costing you ~{annualGhostCost} per year"
- **Known charges row** (when dismissed > 0): count + per-merchant Undo buttons

### Export CSV (Client-Side)
On "Download list" click:
1. Build CSV string with header: `Merchant,Per charge (SEK),Times charged,Already paid (SEK),Est. annual (SEK)`
2. Create Blob with `type: "text/csv"`
3. Trigger download via temporary anchor element
4. Revoke object URL
No backend call. Exports active (non-dismissed) ghosts only.

### Regular Section
- Heading: "Expected bills that keep coming"
- Intro: "These look intentional — bills you likely recognise."

### Summary Panel — 3 Stat Cards

| Card | Primary | Subtext | Extra |
|------|---------|---------|-------|
| "Likely forgotten" | `totalGhostSpend` (monthly) | "Estimated monthly ghost cost" | `~{annualGhostCost}/year` in amber bold |
| "Found in your file" | total ghost + regular spend | "Total across all repeat charges in this file" | — |
| "Transactions checked" | `totalTransactionsAnalyzed` | "{totalRecurringCharges} repeat patterns identified" | — |

### Timeline Panel
8 most recent transactions from all groups, sorted by date descending. Each card: merchant name · amount in SEK · date as "MMM D". Horizontal scrollable grid.

### Cross-Upload Comparison
After each successful analysis (`useEffect` on `analysisState.result`):
- Save current ghost merchant names to `ghostbill_prev_ghosts` in localStorage
- Save AFTER computing `isNewThisUpload` (so new ghosts are correctly identified on the next run)

### Dev Mode (`?dev=1`)
Skip API calls, use inline mock with Netflix (ghost) and City Utilities (regular). Show "Dev" pill badge.

### States
- **Loading:** skeleton loaders (no spinner-only UI)
- **Empty:** GhostIcon at 48px (faded amber) + "No ghosts found yet — upload a bank file to scan for forgotten charges"
- **Error:** CSS-only toast, `aria-live="assertive"`, dismissible

### Accessibility and Responsiveness
- `aria-live="polite"` for results/loading; `aria-live="assertive"` for errors
- Keyboard-accessible; visible focus states
- Mobile: tap-to-upload, single-column; Desktop: drag-and-drop, hover states

---

## Tests Required

### Backend Parity (Mandatory)
CSV, XLSX, and JSON results for semantically equivalent input must be structurally, semantically, and deterministically equivalent.

### PDF Orchestrator Tests (3 Required)

**Precedence:** PDF matching both SequentialTable and RegexRow strategies → must use SequentialTable rows exclusively.

**Failure:** PDF with no machine-readable text → must throw `PARSE_ERROR`. No partial result.

**No-mixing:** multi-page PDF where page 1 matches strategy A and page 2 matches only strategy B → strategy A rows for all pages.

---

## Deliverables

### Backend
All 17 new component files. Only modify the explicitly allowed existing files.

### Frontend
Return only these files, fully functional:
1. `frontend/ghostbill-ui/src/App.tsx`
2. `frontend/ghostbill-ui/src/App.css`
3. `frontend/ghostbill-ui/src/useFileUpload.ts`
4. `frontend/ghostbill-ui/src/useAnalysisMemo.ts`
5. `frontend/ghostbill-ui/src/useDismissed.ts`
6. `frontend/ghostbill-ui/src/usePrevGhosts.ts`
7. `CHANGELOG.md` (max 5 lines)

**Rules for all deliverables:**
- No explanations or markdown outside code files
- No placeholder TODOs or partial implementation
- Must compile without TypeScript errors or console warnings
- Backend tests must pass

---

## Acceptance Criteria

### Backend
- [ ] CSV behavior unchanged
- [ ] XLSX parses and produces equivalent results to equivalent CSV
- [ ] JSON parses with correct alias mapping; unsupported shapes return `PARSE_ERROR`
- [ ] Text-based PDFs parse via strategy pattern
- [ ] Unsupported file extension → `UNSUPPORTED_FORMAT`
- [ ] Malformed supported file → `PARSE_ERROR`
- [ ] PDF precedence, failure, and no-mixing tests pass
- [ ] CSV/XLSX/JSON parity tests pass

### Frontend
- [ ] Hero section: exact headline, subtitle, format chips
- [ ] Upload → skeleton loaders → animated results in SEK
- [ ] Rows staggered by rank (70ms × rank)
- [ ] "Already paid" display on every merchant row
- [ ] Annual `~X/yr` projection on ghost rows in amber
- [ ] Ghost section: eyebrow with GhostIcon, heading, intro, left-border accent
- [ ] Ghost callout banner shows active ghost count + annual cost
- [ ] "Download list" button downloads CSV of active ghosts; no backend call
- [ ] Each ghost row has "I know about this" dismiss button
- [ ] Dismissed ghosts appear in "Known charges" section with Undo
- [ ] Dismissed state persists across reloads and re-uploads
- [ ] First upload: no "New this scan" badges
- [ ] Second upload: new ghosts show "New this scan" badge
- [ ] "New · may be a trial" shown only when `isNewGhost && !isNewThisUpload`
- [ ] 3 stat cards with correct labels and values
- [ ] Timeline panel with up to 8 cards
- [ ] Empty state: GhostIcon (faded amber), correct text
- [ ] Error toasts for all error codes
- [ ] Mobile and desktop responsive
- [ ] Dev mode: Netflix + City Utilities, "Dev" badge
- [ ] No TypeScript errors, no console warnings

### End-to-End
- [ ] A supported file uploaded from the frontend is analyzed by the backend and results rendered
- [ ] Analysis output is format-agnostic — equivalent data produces equivalent results across formats
