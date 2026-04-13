# Backend Implementation Prompt — Ghostbill Multi-Format Support

## Role
You are a backend developer extending the Ghostbill ASP.NET Core API. Your task is to add multi-format file parsing support (XLSX, JSON, PDF) to an existing pipeline that already handles CSV, without changing any existing behavior.

---

## Immutable Constraints — Read Before Anything Else

### Must Not Change
- Existing business logic behavior
- `CsvParsingService` internals, signature, or execution order (legacy-stable, source of truth for CSV)
- Existing API route, request field names, parameter list, or DTO shapes
- Existing pipeline order or semantics
- Observable CSV output (ordering, filtering, normalization, validation, error surface)

### Allowed Existing File Modifications (Additive Only)
- Controller/orchestration entrypoint — integration wiring only
- `Program.cs` — DI registration only
- `Ghostbill.Api.csproj` — package references only
- Backend test project files — tests only

**No other existing files may be modified without explicit approval.**

### Blocked Actions
- Any change to `CsvParsingService` internals or signature
- Any business logic inside parser implementations
- Any change to pipeline order or semantics
- Any change to API route, request field, parameter list, or DTO shape
- Any OCR-based PDF implementation
- Any non-deterministic or time-dependent logic
- Any change altering observable CSV output

---

## Product Context

Ghostbill helps users upload a transaction export or bank statement and understand which recurring outgoing charges look forgotten versus expected.

**User-facing intent:**
- Accept `.csv`, `.xlsx`, `.json`, and text-based machine-readable `.pdf` through one consistent analysis pipeline
- Surface recurring outgoing charges as **Ghosts** (forgotten subscriptions) or **Regulars** (expected bills)
- Return equivalent results for equivalent data regardless of input format

**Scope boundaries:**
- Only outgoing expense transactions influence recurring-expense analysis
- Credits, income, refunds, and positive cash-flow entries are excluded — they must not affect Ghost/Regular classification
- The recurring pattern matters, not isolated one-off transactions
- "Ghost" = likely forgotten or overlooked, not fraudulent
- Do not add budgeting, income-tracking, or savings-analysis behavior
- Do not imply OCR support

---

## Architecture Contract (Non-Negotiable)

```
File → Parser → List<Transaction> → Analysis → AnalysisResult
```

- Parsers are translators only — format to transaction model
- Business logic lives exclusively in the analysis layer
- Analysis layer must be format-agnostic: no format-specific branching, no parser metadata passed forward, analysis must not know which parser produced its input
- `CanHandle(extension)` must be pure, deterministic, and side-effect free
- Parsers must not alter original data meaning — only format translation (`string → DateTime`, `string → decimal`) is allowed

---

## Analysis Algorithm Specification (Authoritative)

This defines the exact behavior of `RecurringExpenseAnalysisService`. Do not change these values or logic without explicit approval.

### Scope Filter
Only negative-amount transactions are processed. Positive entries (income, credits, refunds) are filtered out before grouping and must not affect any classification.

### Merchant Normalization — Grouping Key
Transactions are grouped by a normalized description key:
1. Convert to uppercase
2. Strip all punctuation, digits, and non-letter symbols: `[^A-ZÅÄÖ ]`
3. Collapse consecutive whitespace to a single space

**Display name:** use the most frequent raw (un-normalized) description variant within the group. If tied, use the chronologically earliest. "Netflix Inc." and "NETFLIX" merge into one group; the more frequent raw string is shown.

### Group Filters
Applied before classification. Groups failing either filter are silently discarded — no user-facing error or message:

| Filter | Rule |
|--------|------|
| Minimum occurrences | ≥ 2 transactions required |
| Cadence bounds | Average interval must be **7–40 days**; outside this range → discard |

### Classification Thresholds (Exact Values)

```
amountVariance   = (max_amount - min_amount) / mean_amount
intervalVariance = max_interval_days - min_interval_days

Ghost:   occurrences ≥ 3  AND  amountVariance ≤ 0.03  AND  intervalVariance ≤ 5
Regular: amountVariance ≤ 0.35  AND  intervalVariance ≤ 12
Neither: silently discarded — not shown to user
```

`mean_amount` = arithmetic mean of all amounts in the group (absolute values).

### Trend Data
Each group produces a `trend` array for the frontend sparkline. One entry per calendar month:

```
TrendPoint { label: "YYYY-MM", amount: decimal }
// amount = sum of that month's transaction amounts
// ordered chronologically
```

### Output Sort Order
Within each classification bucket (ghosts / regulars):
1. `TotalAmount` descending (highest cumulative spend first)
2. `Merchant` ascending, case-insensitive A–Z (tiebreaker)

---

## Glossary

| Term | Definition |
|------|-----------|
| Parse | Convert a file into `List<Transaction>` |
| Extract | Pull raw rows from a document (PDF strategy step before parsing) |
| Observable behavior | What an end user or test sees; implementation details are not observable |
| Deterministic | Same input → identical output on every run |
| Ghost | Recurring charge classified as likely forgotten |
| Regular | Recurring charge classified as expected/intentional |

---

## New Components — Exact Paths and Namespaces

All shared helpers must exist only under `Ghostbill.Api.Parsing.Shared`. Do not locate shared logic inside parser implementation files. `ParseDiagnostics` is out of scope and must not be introduced.

### Abstractions and Resolution

| # | Path | Namespace |
|---|------|-----------|
| 1 | `Parsing/Abstractions/ITransactionFileParser.cs` | `Ghostbill.Api.Parsing.Abstractions` |
| 2 | `Parsing/Resolution/ParserResolutionService.cs` | `Ghostbill.Api.Parsing.Resolution` |

### Parsers

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

### Shared Helpers

| # | Path | Namespace | Purpose |
|---|------|-----------|---------|
| 11 | `Parsing/Shared/HeaderDetectionService.cs` | `Ghostbill.Api.Parsing.Shared` | Header row detection |
| 12 | `Parsing/Shared/ColumnMappingService.cs` | `Ghostbill.Api.Parsing.Shared` | Field-to-column mapping |
| 13 | `Parsing/Shared/ValueParsingService.cs` | `Ghostbill.Api.Parsing.Shared` | Date and amount parsing |
| 14 | `Parsing/Shared/RowMaterializationService.cs` | `Ghostbill.Api.Parsing.Shared` | Row → Transaction conversion |
| 15 | `Parsing/Shared/PdfRowFilter.cs` | `Ghostbill.Api.Parsing.Shared` | PDF header/noise filtering |
| 16 | `Parsing/Shared/HeaderNormalization.cs` | `Ghostbill.Api.Parsing.Shared` | Header string normalization |
| 17 | `Parsing/Shared/ParsingAliases.cs` | `Ghostbill.Api.Parsing.Shared` | Accepted column header aliases |

All paths are relative to `backend/src/Ghostbill.Api/`.

---

## Component Specifications

### `ITransactionFileParser` (#1)
**Does:** defines parser contract (`CanHandle(extension)`, `Parse(stream)`).
**Must NOT:** contain parsing logic, business logic, or parser resolution.

### `ParserResolutionService` (#2)
**Does:** resolves the correct parser by calling `CanHandle` on each registered parser. Enforces exactly one match.

**Resolution rules:**

| Matches | Outcome |
|---------|---------|
| 0 | `UNSUPPORTED_FORMAT` error |
| 1 | Use that parser |
| >1 | Configuration exception at startup (developer error — must never reach user) |

**Must NOT:** parse files, call analysis services, or depend on DI registration order.

### `CsvFileParserAdapter` (#3)
**Does:** delegates to `CsvParsingService` and returns result unchanged.
**Hard rule:** pure pass-through only. Any transformation requires explicit approval.
**Must NOT:** transform, filter, reorder, reinterpret, normalize, or remap CSV output in any way.

### `ExcelParsingService` (#4)
**Does:** reads first worksheet via ClosedXML. Converts row/cell values to `List<string[]>`. Uses shared helpers for header detection, column mapping, value parsing, and row materialization. Returns `ParseResult`.
**Must NOT:** perform business logic, call analysis services, or introduce format-specific analysis branching.

### `JsonParsingService` (#5)
**Does:** reads UTF-8 or UTF-8 BOM JSON. Supports exactly two JSON root shapes:
- Top-level array of transaction-like objects
- Top-level object with a `transactions` array

Maps deterministic property aliases to the shared transaction model. Returns `ParseResult`.

**JSON property aliases:**

| Field | Accepted keys |
|-------|--------------|
| Date | `date`, `transactionDate`, `postedDate` |
| Description | `description`, `text`, `merchant`, `name` |
| Amount | `amount`, `value` |

**Must NOT:** guess arbitrary nested JSON structures, perform business logic, or use non-deterministic field matching. Unsupported shapes → `PARSE_ERROR`.

### `PdfParsingService` (#6) — Orchestrator Only
**Does:**
- Opens `PdfDocument` and runs strategies in fixed precedence: `ColumnLayout` → `SequentialTable` → `RegexRow`
- Document-level "first winner": if strategy N yields non-empty rows from any page, stop and use those rows for the entire document
- Deduplicates rows across pages using a hash key before materialization
- Delegates row materialization to `RowMaterializationService`
- Throws `ParsingException("PARSE_ERROR")` if no strategy yields a non-empty result

**Must NOT:** contain extraction logic inline (no bounding-box math, regex patterns, or page iteration), apply per-page strategy selection, register strategy classes in DI, use OCR, or perform business logic.

### `IPdfTransactionExtractionStrategy` (#7)

```csharp
public interface IPdfTransactionExtractionStrategy
{
    string Name { get; }
    bool TryExtract(PdfDocument document, out IReadOnlyList<IReadOnlyList<string>> rows);
}
```

Each strategy:
- Iterates all pages internally
- Returns `true` + non-empty rows on success; `false` + empty on failure
- Must be deterministic for the same PDF input
- Must be `sealed partial class` (required for `[GeneratedRegex]` source generation)
- Must not be registered in DI — instantiated directly in `PdfParsingService`

### `ColumnLayoutPdfStrategy` (#8)
**Does:** bounding-box word-position extraction. Groups page words by Y-coordinate into rows, extracts fields by X-coordinate range (booking date, transaction date, value date, description, amount). Targets column-layout bank statements (Swedbank-style).

Owns: bounding-box constants, row-grouping tolerance, `JoinWords`, `FirstNonEmpty`, `PdfRow` inner record, `DatePatternRegex`.

### `SequentialTablePdfStrategy` (#9)
**Does:** regex-based sequential extraction. Finds first date in page text, then matches records capturing: date, description, TXN ID, type (Debit/Credit), amount, currency, balance. Targets structured table exports with explicit TXN IDs.

Owns: `AnyDateRegex`, `SequentialRecordRegex`.

### `RegexRowPdfStrategy` (#10)
**Does:** generic text-line fallback. Two regex passes: one per line, one on whitespace-normalized full-page text. Last resort for any machine-readable date/description/amount pattern.

Owns: `TransactionLineRegex`, `WhitespaceRegex`.

### `PdfRowFilter` (#15)
**Does:**
- `LooksLikeHeader(string value) → bool` — detects Swedish and generic bank statement header tokens
- `LooksLikeNoise(string value) → bool` — detects document-level noise (account holder lines, balance lines, etc.)

Used by all PDF extraction strategies to filter candidate rows before yielding them.

### Shared Helpers (#11–17)

**`HeaderDetectionService`:** scans first 20 rows for recognized alias matches; requires 2 of 3 field types (date, description, amount) to confirm a header row.

**`HeaderNormalization`:** lowercase + trim + diacritic-strip applied to header cell strings before alias comparison.

**`ParsingAliases`:** authoritative column header alias lists. Files with headers not matching these aliases will fail to map.

| Field | Accepted aliases |
|-------|----------------|
| Date | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference` |
| Amount | `belopp`, `amount`, `value` |

**`ValueParsingService` — Date formats:**
`yyyy-MM-dd`, `dd/MM/yyyy`, `MM/dd/yyyy`, `yyyyMMdd`, `dd MMM yyyy`, and common variants.
Cultures: invariant, `sv-SE`, `en-US`.

**`ValueParsingService` — Amount parsing:**
Handles currency symbols (`$`, `€`, `£`), parentheses for negatives `(100.00) = -100.00`, locale-aware decimal/thousand separators.

**Must NOT** (all shared helpers): depend on `CsvParsingService`, be injected into CSV parser internals, or contain analysis logic.

---

## Error Contract (Exact — Must Not Change)

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
| `PARSE_ERROR` | Parser found but throws, or input shape is unsupported for that format |
| `NO_DATA_FOUND` | Preserve existing CSV behavior only — do not extend to other formats |

**Rules:**
- No parser exception may leak unhandled past the controller boundary
- No format-specific error branching allowed
- CSV error behavior must not change

---

## Temp File Cleanup (Mandatory)
If a temp file is created, it must always be deleted in a `finally` block. Applies to all exit paths where the file exists: success, unsupported format after save, parse error, empty/no-data. Cleanup is not required if validation fails before file creation.

---

## Library Constraints

| Purpose | Library |
|---------|---------|
| XLSX parsing | **ClosedXML** |
| PDF text extraction | **UglyToad.PdfPig** |
| OCR | **Not allowed** |

---

## Tests Required

### Parity (Mandatory)
CSV, XLSX, and JSON results for semantically equivalent input must be structurally, semantically, and deterministically equivalent. Must be proven by automated comparison tests — parity cannot be asserted "by design."

PDF: deterministic for the same fixture file. Where transaction data is extractable from both PDF and structured formats, results should be equivalent.

### PDF Orchestrator Tests (3 Required)

**Precedence test:** given a PDF whose content matches both `SequentialTablePdfStrategy` and `RegexRowPdfStrategy`, the result must use SequentialTable rows exclusively. `RegexRow` must not be invoked when an earlier strategy succeeds.

**Failure test:** a PDF with no machine-readable text (only header/noise) must throw `PARSE_ERROR`. No partial result, no empty transaction list returned.

**No-mixing test:** a multi-page PDF where page 1 matches strategy A and page 2 would only match strategy B must produce rows from strategy A for all pages. The document-level winner applies uniformly — no per-page switching.

---

## Deliverables

Return all 17 new component files plus any required test files. Only modify the explicitly allowed existing files (controller, `Program.cs`, `.csproj`, test project). Return no explanations or markdown outside of code files.
