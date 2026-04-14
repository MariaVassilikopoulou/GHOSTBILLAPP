# Fullstack Implementation Prompt — Ghostbill (v2)

## Role

You are a fullstack developer implementing the complete Ghostbill application. This prompt is self-contained and authoritative — follow it entirely. It supersedes the specialized backend and frontend prompts where they conflict.

Your task: build or extend Ghostbill so it accepts transaction files in four formats, analyzes repeated outgoing expenses, and presents the results in a polished React UI.

---

## Implementation Priorities

Correctness > compatibility > determinism > performance > polish
Determinism > creativity
Simplicity > abstraction

## Execution Sequence (Mandatory)

Implement in this order unless an existing codebase makes a step unnecessary:

1. Inventory the workspace and state any missing baseline explicitly
2. If the repo is greenfield, scaffold the required backend/frontend structure first
3. Define backend contracts and analysis before UI work
4. Implement and verify CSV first; add CSV regression tests before XLSX/JSON/PDF
5. Add parsers in this order: XLSX → JSON → PDF
6. Wire the API entrypoint only after parser behavior is stable
7. Build the frontend against the verified API contract
8. Run backend build/tests and frontend install/build before final output

---

## Product Context

### What Ghostbill Does

Ghostbill is an expense-awareness tool that helps users identify repeated outgoing charges they may have forgotten about — especially subscriptions and repeat expenses that quietly drain money over time.

The user uploads a bank file (`.csv`, `.xlsx`, `.json`, or text-based `.pdf`). Ghostbill analyzes the transactions and surfaces:

- **Ghosts** — consistent timing and amount; likely a forgotten subscription
- **Regulars** — expected variation; bills the user probably recognises

### Core Product Rules

- Focus on repeated outgoing expenses only — not income, savings, or general finance
- Credits, income, refunds, and positive cash-flow entries are excluded from all analysis
- "Ghost" = likely forgotten or overlooked, not fraudulent
- The repeated pattern matters — isolated one-off transactions are not surfaced
- Equivalent transaction data must produce equivalent analysis results regardless of format

### What the User Sees

- A ghost section with a callout banner, merchant list, export button, and dismiss controls
- A regular section with a merchant list
- A "New this scan" badge on ghosts that weren't in the previous upload's ghost list
- A "New · may be a trial" badge on ghost merchants whose first charge was within the last 60 days
- A "Known charges" section for dismissed ghosts with individual undo controls
- A timeline panel with the 8 most recent individual repeated charges
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

| Term                | Definition                                                          |
| ------------------- | ------------------------------------------------------------------- |
| Parse               | Convert a file into `List<Transaction>`                             |
| Extract             | Pull raw rows from a document (PDF step before parsing)             |
| Ghost               | Recurring charge classified as likely forgotten                     |
| Regular             | Recurring charge classified as expected/intentional                 |
| Observable behavior | What an end user or test sees                                       |
| Deterministic       | Same input → identical output on every run                          |
| cadenceDays         | Average interval in days between consecutive charges for a merchant |

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

| Code                 | Condition                                                           |
| -------------------- | ------------------------------------------------------------------- |
| `INVALID_FILE`       | File missing, empty, unreadable, or invalid before parsing          |
| `UNSUPPORTED_FORMAT` | No parser found for the file extension                              |
| `PARSE_ERROR`        | Parser found but throws, or input shape unsupported for that format |
| `NO_DATA_FOUND`      | Preserve existing CSV behavior only                                 |

### Currency and Locale

All monetary values assume **SEK**.
Frontend formatter: `Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' })`

---

## Supported Input Formats

| Format | Rule                                                                                                                                                                                                                             |
| ------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CSV    | Existing behavior is the source of truth. Preserve entirely. If no legacy CSV implementation exists in the workspace, create the CSV baseline first using the authoritative greenfield CSV rules below, then treat it as frozen. |
| XLSX   | Same pipeline as CSV. Must produce equivalent results for equivalent tabular content. Use **ClosedXML**.                                                                                                                         |
| JSON   | Two accepted root shapes: top-level array, or object with `transactions` array. Reject other shapes with `PARSE_ERROR`.                                                                                                          |
| PDF    | Text-based, machine-readable only. No OCR. Uses named-strategy pattern. `PARSE_ERROR` if no rows extractable.                                                                                                                    |

### Greenfield Scaffolding Rule

If the workspace does **not** contain an existing Ghostbill backend/frontend baseline:

- scaffold the required projects first
- create the missing contracts and pipeline in the required paths
- create `CsvParsingService` before any non-CSV parser
- treat the newly created CSV implementation as frozen for the rest of the build

Do **not** assume a legacy codebase exists when the workspace is empty or prompt-only.

### CSV Parsing Specification (Authoritative for Greenfield Scaffolding)

If no legacy `CsvParsingService` exists in the repository, implement CSV parsing with the following behavior and treat it as the source of truth for CSV.

#### Delimiter Detection

- Candidate delimiters: `;`, `,`, `\t`
- Do **not** detect delimiter from only the first non-empty line
- Inspect up to the first **12 non-empty trimmed lines**
- For each candidate delimiter:
  - parse each sampled line with CSV quote handling
  - compute `multiColumnRows` (rows with **>= 3 columns**), `averageColumns`, and `variability` (`maxColumns - minColumns`)
- Pick the delimiter by:
  1. highest `multiColumnRows`
  2. highest `averageColumns`
  3. lowest `variability`
- If no candidate produces a strong signal, default to **comma `,`**
- Never default to tab solely because the first line has no delimiters

#### Leading Metadata / Preamble Rows

- CSV files may contain one or more non-tabular lines before the actual header row
- This must **not** trigger false headerless fallback or single-column failure
- Header detection must inspect the first **20 parsed rows** after delimiter detection

#### Header Detection and Positional Fallback

- Use the same alias-based header detection rules as XLSX
- If a header row is found, map by aliases
- If no header row is found, use positional fallback:
  - column 0 = Date
  - column 1 = Description
  - column 2 = Amount
- Only raise a column-count parsing error when parsed rows truly expose fewer than 3 columns **after correct delimiter detection**

#### CSV Field Handling

- Support quoted fields
- Support escaped double quotes inside quoted fields (`""`)
- Ignore blank lines
- Preserve delimiters that appear inside quoted values

#### Determinism and Parity

- Equivalent CSV, XLSX, and JSON tabular data must produce equivalent `AnalysisResult`
- CSV parsing must be deterministic across runs

#### Mandatory CSV Tests

1. Comma-separated CSV parses correctly
2. Semicolon-separated CSV parses correctly
3. CSV with a leading metadata/title line before the header parses correctly
4. CSV with quoted fields containing delimiters parses correctly
5. Equivalent CSV/XLSX/JSON inputs produce equivalent analysis output

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

| #   | Path                                             | Namespace                            |
| --- | ------------------------------------------------ | ------------------------------------ |
| 1   | `Parsing/Abstractions/ITransactionFileParser.cs` | `Ghostbill.Api.Parsing.Abstractions` |
| 2   | `Parsing/Resolution/ParserResolutionService.cs`  | `Ghostbill.Api.Parsing.Resolution`   |

**Parsers:**

| #   | Path                                                   | Namespace                       |
| --- | ------------------------------------------------------ | ------------------------------- |
| 3   | `Parsing/Parsers/CsvFileParserAdapter.cs`              | `Ghostbill.Api.Parsing.Parsers` |
| 4   | `Parsing/Parsers/ExcelParsingService.cs`               | `Ghostbill.Api.Parsing.Parsers` |
| 5   | `Parsing/Parsers/JsonParsingService.cs`                | `Ghostbill.Api.Parsing.Parsers` |
| 6   | `Parsing/Parsers/PdfParsingService.cs`                 | `Ghostbill.Api.Parsing.Parsers` |
| 7   | `Parsing/Parsers/IPdfTransactionExtractionStrategy.cs` | `Ghostbill.Api.Parsing.Parsers` |
| 8   | `Parsing/Parsers/ColumnLayoutPdfStrategy.cs`           | `Ghostbill.Api.Parsing.Parsers` |
| 9   | `Parsing/Parsers/SequentialTablePdfStrategy.cs`        | `Ghostbill.Api.Parsing.Parsers` |
| 10  | `Parsing/Parsers/RegexRowPdfStrategy.cs`               | `Ghostbill.Api.Parsing.Parsers` |

**Shared Helpers:**

| #   | Path                                          | Namespace                      | Purpose                        |
| --- | --------------------------------------------- | ------------------------------ | ------------------------------ |
| 11  | `Parsing/Shared/HeaderDetectionService.cs`    | `Ghostbill.Api.Parsing.Shared` | Header row detection           |
| 12  | `Parsing/Shared/ColumnMappingService.cs`      | `Ghostbill.Api.Parsing.Shared` | Field-to-column mapping        |
| 13  | `Parsing/Shared/ValueParsingService.cs`       | `Ghostbill.Api.Parsing.Shared` | Date and amount parsing        |
| 14  | `Parsing/Shared/RowMaterializationService.cs` | `Ghostbill.Api.Parsing.Shared` | Row → Transaction conversion   |
| 15  | `Parsing/Shared/PdfRowFilter.cs`              | `Ghostbill.Api.Parsing.Shared` | PDF header/noise filtering     |
| 16  | `Parsing/Shared/HeaderNormalization.cs`       | `Ghostbill.Api.Parsing.Shared` | Header string normalization    |
| 17  | `Parsing/Shared/ParsingAliases.cs`            | `Ghostbill.Api.Parsing.Shared` | Accepted column header aliases |

### Column Header Aliases

| Field       | Accepted aliases                                                                                      |
| ----------- | ----------------------------------------------------------------------------------------------------- |
| Date        | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference`                     |
| Amount      | `belopp`, `amount`, `value`                                                                           |

### JSON Property Aliases

| Field       | Accepted keys                             |
| ----------- | ----------------------------------------- |
| Date        | `date`, `transactionDate`, `postedDate`   |
| Description | `description`, `text`, `merchant`, `name` |
| Amount      | `amount`, `value`                         |

### Analysis Algorithm (Authoritative — Do Not Change)

**Scope:** negative-amount transactions only. Positive entries are filtered out before grouping.

**Merchant normalization (grouping key):**

1. Uppercase
2. Strip punctuation, digits, non-letter symbols: `[^A-ZÅÄÖ ]`
3. Collapse whitespace

**Display name:** most frequent raw (un-normalized) variant in the group. If tied, use chronologically earliest.

**Group filters** (silently discard failing groups):

| Filter              | Rule                           |
| ------------------- | ------------------------------ |
| Minimum occurrences | ≥ 2                            |
| Cadence bounds      | Average interval **7–40 days** |

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

### Component Implementation Details

**`ITransactionFileParser`:** exact interface (do not alter):

```csharp
bool CanHandle(string extension);
IReadOnlyList<Transaction> Parse(Stream stream, string fileName);
```

**`ExcelParsingService`:** required ClosedXML access pattern:

```
workbook.Worksheet(1)           ← 1-based index
worksheet.RangeUsed()           ← null if empty → return []
range.RowsUsed()                ← automatically skips empty rows
cell.GetFormattedString()       ← NOT .Value?.ToString()
```

**`JsonParsingService`:** does NOT use `RowMaterializationService`. Creates Transaction objects directly.

- Stream: `new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)`
- Number amounts: `element.GetDecimal().ToString(CultureInfo.InvariantCulture)` before `ParseAmount`
- Missing required property alias → throw `PARSE_ERROR` (does not skip; whole file fails)
- Property matching: `StringComparer.OrdinalIgnoreCase`

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

### Parsing Reference Specification

**Encoding handling** (XLSX and new text parsers; CSV frozen; JSON/PDF handle automatically):

- Try UTF-8 strict (`throwOnInvalidBytes: true`) → on `DecoderFallbackException` → retry Windows-1252 (`Encoding.GetEncoding(1252)`)
- Register `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before first use
- JSON: `StreamReader` with `detectEncodingFromByteOrderMarks: true` handles BOM automatically
- XLSX: ClosedXML handles encoding; PDF: PdfPig handles Unicode — no action needed

**CSV encoding note for greenfield builds:** when creating `CsvParsingService` from scratch, apply the same UTF-8-strict then Windows-1252 fallback behavior unless the workspace already contains a legacy CSV implementation that must remain untouched.

**Headerless file fallback** (XLSX; CSV frozen):

- `HeaderDetectionService` returns `null` → positional: col 0=Date, col 1=Description, col 2=Amount
- Fewer than 3 columns → throw `PARSE_ERROR`. JSON has no header concept; use property aliases always.

**Row materialization failure policy** (`RowMaterializationService` — XLSX and PDF only; JSON does NOT use this):

- Unparseable date or amount → catch `FormatException` → skip row → add to `skippedReasons`
- Missing description → empty string; do not skip. Never throw on a single bad row.

**JSON root shapes** (case-insensitive; any other shape → `PARSE_ERROR`):

```json
Shape A: [{ "date": "2024-01-15", "description": "Netflix", "amount": -149.00 }]
Shape B: { "transactions": [{ "date": "2024-01-15", "description": "Netflix", "amount": -149.00 }] }
```

**`PdfRowFilter` exact tokens** (post `HeaderNormalization.Normalize()` = lowercase+trim+diacritic-strip):

| Method            | Match  | Tokens                                                                                               |
| ----------------- | ------ | ---------------------------------------------------------------------------------------------------- |
| `LooksLikeHeader` | Exact  | `beskrivning`, `referens`, `belopp`, `bokfortsaldo`, `transaktionsdag`, `bokforingsdag`, `valutadag` |
| `LooksLikeNoise`  | Prefix | `saldo`, `kontohavare`, `privatkonto`, `transaktioner`, `skapad`                                     |
| `LooksLikeNoise`  | Exact  | `sek`                                                                                                |

**`ColumnLayoutPdfStrategy` constants:**

```
RowTolerance=2.5, BookingDateLeft=145, BookingDateRight=205
TransactionDateLeft=205, TransactionDateRight=260, ValueDateLeft=260, ValueDateRight=312
DescriptionLeft=312, DescriptionRight=438, AmountLeft=438, AmountRight=490
Word filter: BoundingBox.Left >= 141 | Date validation: ^\d{4}-\d{2}-\d{2}$
Date priority: transactionDate → bookingDate → valueDate
```

**`SequentialTablePdfStrategy` patterns:**

```
AnyDateRegex: \d{4}-\d{2}-\d{2}
SequentialRecordRegex: (?<date>\d{4}-\d{2}-\d{2})(?<description>.*?)(?<transactionId>TXN\d+)(?<type>Debit|Credit)(?<amount>[\+\-]?\d[\d,\.]*)(?<currency>[A-Z]{3})(?<balance>[\+\-]?\d[\d,\.]*)(?=(?:\d{4}-\d{2}-\d{2})|$)
```

**`RegexRowPdfStrategy` patterns** (two passes: raw lines then whitespace-normalized page text):

```
WhitespaceRegex: \s+
TransactionLineRegex: (?<date>\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})\s+(?<description>[A-Za-z][A-Za-z\s&'\-]+?)\s+(?<amount>\(?-?[$€£]?\d[\d,\.]*\)?)\s*(?=(?:\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})|$)
```

All PDF strategy classes: `sealed partial class`, `[GeneratedRegex]` source generation, not in DI.

### Libraries

| Purpose             | Library             |
| ------------------- | ------------------- |
| XLSX                | **ClosedXML**       |
| PDF text extraction | **UglyToad.PdfPig** |
| OCR                 | **Not allowed**     |

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

| Key                     | Type       | Purpose                                       |
| ----------------------- | ---------- | --------------------------------------------- |
| `ghostbill_dismissed`   | `string[]` | Merchant names the user has marked as known   |
| `ghostbill_prev_ghosts` | `string[]` | Ghost merchant names from the previous upload |

Managed by two custom hooks: `useDismissed` and `usePrevGhosts`.

### MerchantRowModel Computed Fields

| Field             | Formula                                                                                  |
| ----------------- | ---------------------------------------------------------------------------------------- |
| `rank`            | 1-based position sorted by `totalAmount` desc                                            |
| `annualCost`      | `Math.round(averageAmount × (365 / cadenceDays))`                                        |
| `monthsRunning`   | `Math.max(1, Math.round((occurrences × cadenceDays) / 30))`                              |
| `isNewGhost`      | `classification === "ghost" && daysSince(firstChargeDate) <= 60`                         |
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

| Card                   | Primary                     | Subtext                                              | Extra                                   |
| ---------------------- | --------------------------- | ---------------------------------------------------- | --------------------------------------- |
| "Likely forgotten"     | `totalGhostSpend` (monthly) | "Estimated monthly ghost cost"                       | `~{annualGhostCost}/year` in amber bold |
| "Found in your file"   | total ghost + regular spend | "Total across all repeat charges in this file"       | —                                       |
| "Transactions checked" | `totalTransactionsAnalyzed` | "{totalRecurringCharges} repeat patterns identified" | —                                       |

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
