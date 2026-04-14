# Backend Implementation Prompt — Ghostbill Multi-Format Support (v2)

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

Ghostbill helps users upload a bank file and understand which repeated outgoing charges look forgotten versus expected.

**User-facing intent:**

- Accept `.csv`, `.xlsx`, `.json`, and text-based machine-readable `.pdf` through one consistent analysis pipeline
- Surface repeated outgoing charges as **Ghosts** (forgotten subscriptions) or **Regulars** (expected bills)
- Return equivalent results for equivalent data regardless of input format

**Scope boundaries:**

- Only outgoing expense transactions influence repeated-expense analysis
- Credits, income, refunds, and positive cash-flow entries are excluded
- The repeated pattern matters, not isolated one-off transactions
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
- Analysis layer must be format-agnostic: no format-specific branching, no parser metadata passed forward
- `CanHandle(extension)` must be pure, deterministic, and side-effect free
- Parsers must not alter original data meaning — only format translation is allowed

## Execution Sequence (Mandatory)

Implement in this order unless an existing codebase makes a step unnecessary:

1. Detect whether a legacy `CsvParsingService` already exists
2. If it does not, create it first and freeze it as the CSV source of truth
3. Implement backend contracts and analysis before non-CSV parsers
4. Validate CSV with tests before XLSX, JSON, or PDF
5. Add XLSX and JSON parity next
6. Add the PDF orchestrator and strategies last
7. Wire controller integration and DI after parser behavior is stable
8. Run build and test verification before considering the work complete

## Greenfield Scaffolding Rule

If the workspace does **not** contain the existing Ghostbill backend assumed by this prompt:

- scaffold the missing ASP.NET Core API structure first
- create the required paths and contracts exactly as specified
- create `CsvParsingService` before any non-CSV parser
- treat the new CSV implementation as frozen baseline behavior for the rest of the work

Do **not** assume the repository already contains the legacy backend when the workspace is empty or prompt-only.

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

**Display name:** use the most frequent raw (un-normalized) description variant within the group. If tied, use the chronologically earliest.

### Group Filters

Applied before classification. Groups failing either filter are silently discarded — no user-facing error or message:

| Filter              | Rule                                                                 |
| ------------------- | -------------------------------------------------------------------- |
| Minimum occurrences | ≥ 2 transactions required                                            |
| Cadence bounds      | Average interval must be **7–40 days**; outside this range → discard |

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

Each group produces a `trend` array. One entry per calendar month:

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

| Term                | Definition                                                       |
| ------------------- | ---------------------------------------------------------------- |
| Parse               | Convert a file into `List<Transaction>`                          |
| Extract             | Pull raw rows from a document (PDF strategy step before parsing) |
| Observable behavior | What an end user or test sees                                    |
| Deterministic       | Same input → identical output on every run                       |
| Ghost               | Recurring charge classified as likely forgotten                  |
| Regular             | Recurring charge classified as expected/intentional              |

---

## New Components — Exact Paths and Namespaces

All shared helpers must exist only under `Ghostbill.Api.Parsing.Shared`. Do not locate shared logic inside parser implementation files.

### Abstractions and Resolution

| #   | Path                                             | Namespace                            |
| --- | ------------------------------------------------ | ------------------------------------ |
| 1   | `Parsing/Abstractions/ITransactionFileParser.cs` | `Ghostbill.Api.Parsing.Abstractions` |
| 2   | `Parsing/Resolution/ParserResolutionService.cs`  | `Ghostbill.Api.Parsing.Resolution`   |

### Parsers

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

### Shared Helpers

| #   | Path                                          | Namespace                      | Purpose                        |
| --- | --------------------------------------------- | ------------------------------ | ------------------------------ |
| 11  | `Parsing/Shared/HeaderDetectionService.cs`    | `Ghostbill.Api.Parsing.Shared` | Header row detection           |
| 12  | `Parsing/Shared/ColumnMappingService.cs`      | `Ghostbill.Api.Parsing.Shared` | Field-to-column mapping        |
| 13  | `Parsing/Shared/ValueParsingService.cs`       | `Ghostbill.Api.Parsing.Shared` | Date and amount parsing        |
| 14  | `Parsing/Shared/RowMaterializationService.cs` | `Ghostbill.Api.Parsing.Shared` | Row → Transaction conversion   |
| 15  | `Parsing/Shared/PdfRowFilter.cs`              | `Ghostbill.Api.Parsing.Shared` | PDF header/noise filtering     |
| 16  | `Parsing/Shared/HeaderNormalization.cs`       | `Ghostbill.Api.Parsing.Shared` | Header string normalization    |
| 17  | `Parsing/Shared/ParsingAliases.cs`            | `Ghostbill.Api.Parsing.Shared` | Accepted column header aliases |

All paths are relative to `backend/src/Ghostbill.Api/`.

---

## Component Specifications

### `ITransactionFileParser` (#1)

**Does:** defines parser contract. Exact interface (do not alter signatures):

```csharp
public interface ITransactionFileParser
{
    bool CanHandle(string extension);
    IReadOnlyList<Transaction> Parse(Stream stream, string fileName);
}
```

**Must NOT:** contain parsing logic, business logic, or parser resolution.

### `ParserResolutionService` (#2)

**Does:** resolves the correct parser by calling `CanHandle` on each registered parser. Enforces exactly one match.

| Matches | Outcome                            |
| ------- | ---------------------------------- |
| 0       | `UNSUPPORTED_FORMAT` error         |
| 1       | Use that parser                    |
| >1      | Configuration exception at startup |

**Must NOT:** parse files, call analysis services, or depend on DI registration order.

### `CsvFileParserAdapter` (#3)

**Does:** delegates to `CsvParsingService` and returns result unchanged.
**Hard rule:** pure pass-through only.
**Must NOT:** transform, filter, reorder, reinterpret, normalize, or remap CSV output in any way.

### `CsvParsingService` (Greenfield Rule When Legacy Service Is Missing)

If the repository does not already contain `CsvParsingService`, implement it first and freeze its behavior before building XLSX, JSON, or PDF support.

Implement the following CSV behavior exactly:

#### Delimiter Detection

- Candidate delimiters: `;`, `,`, `\t`
- Do **not** inspect only the first non-empty line
- Sample up to the first **12 non-empty trimmed lines**
- For each candidate:
  - parse with CSV quote handling
  - compute `multiColumnRows` (rows producing **>= 3 columns**), `averageColumns`, and `variability` (`maxColumns - minColumns`)
- Pick the delimiter by:
  1. highest `multiColumnRows`
  2. highest `averageColumns`
  3. lowest `variability`
- If no candidate is clearly valid, default to **comma `,`**
- Never default to tab solely because the first line lacks delimiters

#### Leading Metadata / Preamble Rows

- CSV may contain title or metadata lines before the actual header
- Header detection must scan the first **20 parsed rows**
- Leading non-tabular lines must **not** force false headerless parsing or single-column failure

#### Header Detection and Fallback

- Reuse the same alias-driven header detection rules as XLSX
- If header exists: map by alias
- If header does not exist: positional mapping `0=Date, 1=Description, 2=Amount`
- Only throw the “fewer than 3 columns” parsing error when rows truly expose fewer than 3 columns after correct delimiter detection

#### CSV Field Handling

- Support quoted fields
- Support escaped quotes (`""`)
- Ignore blank lines
- Preserve delimiters inside quoted fields

#### Required Greenfield CSV Tests

1. Comma-delimited CSV
2. Semicolon-delimited CSV
3. CSV with one leading metadata/title row before header
4. CSV with quoted fields containing delimiters
5. CSV/XLSX/JSON parity for equivalent data

### `ExcelParsingService` (#4)

**Does:** reads first worksheet via ClosedXML. Uses shared helpers for header detection, column mapping, value parsing, and row materialization.

Required ClosedXML access pattern (exact — deviations produce wrong output):

```
Open:        new XLWorkbook(stream)
Worksheet:   workbook.Worksheet(1)          ← 1-based index
Used range:  worksheet.RangeUsed()          ← null if empty → return []
Rows:        range.RowsUsed()               ← automatically skips empty rows
Cell values: cell.GetFormattedString()      ← NOT .Value?.ToString()
```

**Must NOT:** perform business logic or introduce format-specific analysis branching.

### `JsonParsingService` (#5)

**Does:** reads JSON, maps property aliases to Transaction objects directly — does NOT use `RowMaterializationService`.

Required implementation pattern:

```
Stream reading: new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)
  ← handles BOM automatically; no manual stripping needed
Number amounts: element.GetDecimal().ToString(CultureInfo.InvariantCulture) before ParseAmount
Missing required property alias → throw PARSE_ERROR (does NOT skip the record)
Property matching: StringComparer.OrdinalIgnoreCase
```

Supports exactly two JSON root shapes:

- Top-level array of transaction objects
- Top-level object with a `transactions` array

**JSON property aliases:**

| Field       | Accepted keys                             |
| ----------- | ----------------------------------------- |
| Date        | `date`, `transactionDate`, `postedDate`   |
| Description | `description`, `text`, `merchant`, `name` |
| Amount      | `amount`, `value`                         |

**Must NOT:** guess arbitrary nested JSON structures, use `RowMaterializationService`, or perform business logic. Unsupported shapes → `PARSE_ERROR`.

### `PdfParsingService` (#6) — Orchestrator Only

**Does:**

- Opens `PdfDocument` and runs strategies in fixed precedence: `ColumnLayout` → `SequentialTable` → `RegexRow`
- Document-level "first winner": if strategy N yields non-empty rows from any page, stop and use those rows for the entire document
- Deduplicates rows across pages using a hash key before materialization
- Delegates row materialization to `RowMaterializationService`
- Throws `ParsingException("PARSE_ERROR")` if no strategy yields a non-empty result

**Must NOT:** contain extraction logic inline, apply per-page strategy selection, register strategy classes in DI, use OCR, or perform business logic.

### `IPdfTransactionExtractionStrategy` (#7)

```csharp
public interface IPdfTransactionExtractionStrategy
{
    string Name { get; }
    bool TryExtract(PdfDocument document, out IReadOnlyList<IReadOnlyList<string>> rows);
}
```

Each strategy: iterates all pages internally; returns `true` + non-empty rows on success; `false` + empty on failure; deterministic; `sealed partial class`; not registered in DI.

### `ColumnLayoutPdfStrategy` (#8)

**Does:** bounding-box word-position extraction. Groups page words by Y-coordinate into rows, extracts fields by X-coordinate range. Targets column-layout bank statements (Swedbank-style).

### `SequentialTablePdfStrategy` (#9)

**Does:** regex-based sequential extraction. Finds first date in page text, then matches records capturing: date, description, TXN ID, type (Debit/Credit), amount, currency, balance. Targets structured table exports with explicit TXN IDs.

### `RegexRowPdfStrategy` (#10)

**Does:** generic text-line fallback. Two regex passes: one per line, one on whitespace-normalized full-page text. Last resort for any machine-readable date/description/amount pattern.

### `PdfRowFilter` (#15)

**Does:**

- `LooksLikeHeader(string value) → bool` — detects Swedish and generic bank statement header tokens
- `LooksLikeNoise(string value) → bool` — detects document-level noise

Used by all PDF extraction strategies to filter candidate rows before yielding them.

### Shared Helpers (#11–17)

**`HeaderDetectionService`:** scans first 20 rows for recognized alias matches; requires 2 of 3 field types (date, description, amount) to confirm a header row.

**`HeaderNormalization`:** lowercase + trim + diacritic-strip applied to header cell strings before alias comparison.

**`ParsingAliases`:** authoritative column header alias lists.

| Field       | Accepted aliases                                                                                      |
| ----------- | ----------------------------------------------------------------------------------------------------- |
| Date        | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference`                     |
| Amount      | `belopp`, `amount`, `value`                                                                           |

**`ValueParsingService` — Date formats:**
`yyyy-MM-dd`, `dd/MM/yyyy`, `MM/dd/yyyy`, `yyyyMMdd`, `dd MMM yyyy`, and common variants.
Cultures: invariant, `sv-SE`, `en-US`.

**`ValueParsingService` — Amount parsing:**
Handles currency symbols, parentheses for negatives `(100.00) = -100.00`, locale-aware decimal/thousand separators.

**Must NOT** (all shared helpers): depend on `CsvParsingService`, be injected into CSV parser internals, or contain analysis logic.

---

## Parsing Reference Specification

These values are authoritative. Implement exactly as stated.

### Encoding Handling

Apply to all text-based parsers (XLSX, JSON; CSV is frozen):

- Try UTF-8 with strict validation: `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)`
- On `DecoderFallbackException` → retry as Windows-1252: `Encoding.GetEncoding(1252)`
- Register `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before first use
- XLSX: ClosedXML handles encoding internally — no action needed
- JSON: `StreamReader` with `detectEncodingFromByteOrderMarks: true` — automatic, no manual stripping
- PDF: PdfPig extracts Unicode text — no action needed

If `CsvParsingService` is being created from scratch in a greenfield workspace, apply the same UTF-8-strict then Windows-1252 fallback there as well.

### Headerless File Fallback

Applies to XLSX (CSV is frozen but follows the same pattern):

- If `HeaderDetectionService` returns `null` → positional mapping: column 0 = Date, column 1 = Description, column 2 = Amount
- Fewer than 3 columns → throw `PARSE_ERROR`
- JSON: no header concept; property-name mapping always applies

### Row Materialization Failure Policy (`RowMaterializationService`)

Used by XLSX and PDF only — JSON does NOT use `RowMaterializationService`:

- Unparseable date or amount → catch `FormatException` → skip row → record in `skippedReasons`
- Missing description → empty string; do not skip
- Never throw on a single bad row; always continue processing remaining rows

### JSON Root Shapes — Exact Contract

Property name matching is **case-insensitive**. Any other root shape → `PARSE_ERROR`.

Shape A — top-level array:

```json
[{ "date": "2024-01-15", "description": "Netflix", "amount": -149.0 }]
```

Shape B — object with `transactions` key:

```json
{
  "transactions": [
    { "date": "2024-01-15", "description": "Netflix", "amount": -149.0 }
  ]
}
```

### `PdfRowFilter` — Exact Token Lists

Applied after `HeaderNormalization.Normalize()` (lowercase + trim + diacritic-strip):

| Method            | Match type | Tokens                                                                                               |
| ----------------- | ---------- | ---------------------------------------------------------------------------------------------------- |
| `LooksLikeHeader` | Exact      | `beskrivning`, `referens`, `belopp`, `bokfortsaldo`, `transaktionsdag`, `bokforingsdag`, `valutadag` |
| `LooksLikeNoise`  | Prefix     | `saldo`, `kontohavare`, `privatkonto`, `transaktioner`, `skapad`                                     |
| `LooksLikeNoise`  | Exact      | `sek`                                                                                                |

### PDF Strategy Constants and Regex Patterns (Exact — Required for Correct Extraction)

#### `ColumnLayoutPdfStrategy` — Bounding-Box Constants

```
RowTolerance      = 2.5   // Y-coordinate grouping tolerance
BookingDateLeft   = 145,  BookingDateRight   = 205
TransactionDateLeft = 205, TransactionDateRight = 260
ValueDateLeft     = 260,  ValueDateRight     = 312
DescriptionLeft   = 312,  DescriptionRight   = 438
AmountLeft        = 438,  AmountRight        = 490
Word filter: BoundingBox.Left >= 141 (BookingDateLeft - 4)
Date validation:  ^\d{4}-\d{2}-\d{2}$
Date priority:    transactionDate → bookingDate → valueDate (FirstNonEmpty)
```

#### `SequentialTablePdfStrategy` — Regex Patterns

```
AnyDateRegex:          \d{4}-\d{2}-\d{2}
SequentialRecordRegex: (?<date>\d{4}-\d{2}-\d{2})(?<description>.*?)(?<transactionId>TXN\d+)(?<type>Debit|Credit)(?<amount>[\+\-]?\d[\d,\.]*)(?<currency>[A-Z]{3})(?<balance>[\+\-]?\d[\d,\.]*)(?=(?:\d{4}-\d{2}-\d{2})|$)
```

#### `RegexRowPdfStrategy` — Regex Patterns

Two passes per page: raw lines first, then whitespace-normalized full-page text.

```
WhitespaceRegex:      \s+
TransactionLineRegex: (?<date>\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})\s+(?<description>[A-Za-z][A-Za-z\s&'\-]+?)\s+(?<amount>\(?-?[$€£]?\d[\d,\.]*\)?)\s*(?=(?:\d{4}[-/]\d{2}[-/]\d{2}|\d{2}[-/]\d{2}[-/]\d{4}|[A-Za-z]{3}\s+\d{1,2},?\s+\d{4})|$)
```

All PDF strategy classes: `sealed partial class`, `[GeneratedRegex]` source generation, not registered in DI.

---

## Error Contract (Exact — Must Not Change)

```json
{
  "message": "string",
  "code": "INVALID_FILE | UNSUPPORTED_FORMAT | PARSE_ERROR | NO_DATA_FOUND",
  "details": "string (optional)"
}
```

| Code                 | Condition                                                              |
| -------------------- | ---------------------------------------------------------------------- |
| `INVALID_FILE`       | File missing, empty, unreadable, or invalid before parsing             |
| `UNSUPPORTED_FORMAT` | No parser found for the file extension                                 |
| `PARSE_ERROR`        | Parser found but throws, or input shape is unsupported for that format |
| `NO_DATA_FOUND`      | Preserve existing CSV behavior only — do not extend to other formats   |

---

## Temp File Cleanup (Mandatory)

If a temp file is created, it must always be deleted in a `finally` block on all exit paths.

---

## Library Constraints

| Purpose             | Library             |
| ------------------- | ------------------- |
| XLSX parsing        | **ClosedXML**       |
| PDF text extraction | **UglyToad.PdfPig** |
| OCR                 | **Not allowed**     |

---

## Tests Required

### Parity (Mandatory)

CSV, XLSX, and JSON results for semantically equivalent input must be structurally, semantically, and deterministically equivalent.

### PDF Orchestrator Tests (3 Required)

**Precedence test:** given a PDF whose content matches both `SequentialTablePdfStrategy` and `RegexRowPdfStrategy`, the result must use SequentialTable rows exclusively.

**Failure test:** a PDF with no machine-readable text must throw `PARSE_ERROR`. No partial result, no empty transaction list returned.

**No-mixing test:** a multi-page PDF where page 1 matches strategy A and page 2 would only match strategy B must produce rows from strategy A for all pages.

---

## Deliverables

Return all 17 new component files plus any required test files. Only modify the explicitly allowed existing files (controller, `Program.cs`, `.csproj`, test project). Return no explanations or markdown outside of code files.
