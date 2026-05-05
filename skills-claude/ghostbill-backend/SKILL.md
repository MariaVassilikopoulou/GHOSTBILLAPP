---
name: ghostbill-backend
description: This skill should be used when the user asks to implement or extend the Ghostbill backend only, add multi-format parsing (XLSX, JSON, PDF) to the ASP.NET Core API, build Ghostbill backend components without touching the frontend, or run the backend-only Ghostbill v2 implementation prompt.
version: 2.0.0
argument-hint: [optional notes or scope override]
allowed-tools: [Read, Write, Edit, Glob, Grep, Bash]
---

# Ghostbill Backend Implementation — v2

$ARGUMENTS

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

**Scope boundaries:**
- Only outgoing expense transactions influence repeated-expense analysis
- Credits, income, refunds, and positive cash-flow entries are excluded
- The repeated pattern matters, not isolated one-off transactions
- "Ghost" = likely forgotten or overlooked, not fraudulent
- Do not add budgeting, income-tracking, or savings-analysis behavior

---

## Architecture Contract (Non-Negotiable)

```
File → Parser → List<Transaction> → Analysis → AnalysisResult
```

- Parsers are translators only — format to transaction model
- Business logic lives exclusively in the analysis layer
- Analysis layer must be format-agnostic
- `CanHandle(extension)` must be pure, deterministic, and side-effect free

## Execution Sequence (Mandatory)

1. Detect whether a legacy `CsvParsingService` already exists
2. If it does not, create it first and freeze it as the CSV source of truth
3. Implement backend contracts and analysis before non-CSV parsers
4. Validate CSV with tests before XLSX, JSON, or PDF
5. Add XLSX and JSON parity next
6. Add the PDF orchestrator and strategies last
7. Wire controller integration and DI after parser behavior is stable
8. Run build and test verification before considering the work complete

## Greenfield Scaffolding Rule

If the workspace does **not** contain the existing Ghostbill backend: scaffold missing ASP.NET Core API structure first, create `CsvParsingService` before any non-CSV parser, treat the new CSV implementation as frozen baseline.

---

## Analysis Algorithm Specification (Authoritative)

### Scope Filter

Only negative-amount transactions are processed.

### Merchant Normalization — Grouping Key

1. Convert to uppercase
2. Strip all punctuation, digits, and non-letter symbols: `[^A-ZÅÄÖ ]`
3. Collapse consecutive whitespace to a single space

**Display name:** most frequent raw description variant. If tied, chronologically earliest.

### Group Filters

| Filter | Rule |
|--------|------|
| Minimum occurrences | ≥ 2 transactions required |
| Cadence bounds | Average interval must be **7–40 days** |

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

Each group produces a `trend` array — one entry per calendar month:

```
TrendPoint { label: "YYYY-MM", amount: decimal }
// amount = sum of that month's transaction amounts, ordered chronologically
```

### Output Sort Order

1. `TotalAmount` descending
2. `Merchant` ascending, case-insensitive A–Z (tiebreaker)

---

## New Components — Exact Paths and Namespaces

All paths relative to `backend/src/Ghostbill.Api/`.

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

---

## Component Specifications

### `ITransactionFileParser` (#1)

```csharp
public interface ITransactionFileParser
{
    bool CanHandle(string extension);
    IReadOnlyList<Transaction> Parse(Stream stream, string fileName);
}
```

### `ParserResolutionService` (#2)

| Matches | Outcome |
|---------|---------|
| 0 | `UNSUPPORTED_FORMAT` error |
| 1 | Use that parser |
| >1 | Configuration exception at startup |

### `CsvFileParserAdapter` (#3)

Pure pass-through to `CsvParsingService`. Must NOT transform, filter, reorder, reinterpret, normalize, or remap CSV output in any way.

### `CsvParsingService` (Greenfield Rule When Missing)

#### Delimiter Detection

- Candidate delimiters: `;`, `,`, `\t`
- Sample up to the first **12 non-empty trimmed lines**
- For each candidate: compute `multiColumnRows` (>= 3 cols), `averageColumns`, `variability`
- Pick: 1) highest `multiColumnRows` 2) highest `averageColumns` 3) lowest `variability`
- Default to **comma `,`** if no clear winner; never default to tab

#### Leading Metadata / Preamble Rows

- Header detection must scan the first **20 parsed rows**

#### Header Detection and Fallback

- Header exists: map by alias. No header: positional `0=Date, 1=Description, 2=Amount`
- Only throw "fewer than 3 columns" error when rows truly expose fewer than 3 columns after correct detection

#### CSV Field Handling

- Support quoted fields, escaped quotes (`""`), ignore blank lines, preserve delimiters inside quoted fields

#### Required Greenfield CSV Tests

1. Comma-delimited CSV
2. Semicolon-delimited CSV
3. CSV with one leading metadata/title row before header
4. CSV with quoted fields containing delimiters
5. CSV/XLSX/JSON parity for equivalent data

### `ExcelParsingService` (#4)

Required ClosedXML access pattern (exact):

```
Open:        new XLWorkbook(stream)
Worksheet:   workbook.Worksheet(1)          ← 1-based index
Used range:  worksheet.RangeUsed()          ← null if empty → return []
Rows:        range.RowsUsed()               ← automatically skips empty rows
Cell values: cell.GetFormattedString()      ← NOT .Value?.ToString()
```

### `JsonParsingService` (#5)

Does NOT use `RowMaterializationService`. Creates Transaction objects directly.

```
Stream reading: new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)
Number amounts: element.GetDecimal().ToString(CultureInfo.InvariantCulture) before ParseAmount
Missing required property alias → throw PARSE_ERROR (does NOT skip the record)
Property matching: StringComparer.OrdinalIgnoreCase
```

Supported shapes:
- Shape A: `[{ "date": "2024-01-15", "description": "Netflix", "amount": -149.0 }]`
- Shape B: `{ "transactions": [{ "date": "2024-01-15", "description": "Netflix", "amount": -149.0 }] }`

**JSON property aliases:**

| Field | Accepted keys |
|-------|--------------|
| Date | `date`, `transactionDate`, `postedDate` |
| Description | `description`, `text`, `merchant`, `name` |
| Amount | `amount`, `value` |

### `PdfParsingService` (#6) — Orchestrator Only

- Runs strategies in fixed precedence: `ColumnLayout` → `SequentialTable` → `RegexRow`
- Document-level "first winner": strategy with non-empty rows from any page wins for entire document
- Deduplicates rows across pages using a hash key before materialization
- Throws `ParsingException("PARSE_ERROR")` if no strategy yields a non-empty result
- Must NOT contain extraction logic inline or apply per-page strategy selection

### `IPdfTransactionExtractionStrategy` (#7)

```csharp
public interface IPdfTransactionExtractionStrategy
{
    string Name { get; }
    bool TryExtract(PdfDocument document, out IReadOnlyList<IReadOnlyList<string>> rows);
}
```

Each strategy: iterates all pages internally; `sealed partial class`; not registered in DI.

### `ColumnLayoutPdfStrategy` (#8)

Bounding-box word-position extraction. Groups page words by Y-coordinate into rows, extracts fields by X-coordinate range. Targets column-layout bank statements (Swedbank-style).

### `SequentialTablePdfStrategy` (#9)

Regex-based sequential extraction. Finds first date in page text, then matches records capturing: date, description, TXN ID, type (Debit/Credit), amount, currency, balance.

### `RegexRowPdfStrategy` (#10)

Generic text-line fallback. Two regex passes: one per line, one on whitespace-normalized full-page text. Last resort for any machine-readable date/description/amount pattern.

### Shared Helpers (#11–17)

**`HeaderDetectionService`:** scans first 20 rows; requires 2 of 3 field types (date, description, amount) to confirm a header row.

**`HeaderNormalization`:** lowercase + trim + diacritic-strip applied before alias comparison.

**`ParsingAliases`:**

| Field | Accepted aliases |
|-------|-----------------|
| Date | `transaktionsdag`, `transactiondate`, `bokforingsdag`, `posteddate`, `date`, `valutadag`, `valuedate` |
| Description | `beskrivning`, `description`, `text`, `merchant`, `name`, `referens`, `reference` |
| Amount | `belopp`, `amount`, `value` |

**`ValueParsingService` — Date formats:** `yyyy-MM-dd`, `dd/MM/yyyy`, `MM/dd/yyyy`, `yyyyMMdd`, `dd MMM yyyy`, and common variants. Cultures: invariant, `sv-SE`, `en-US`.

**`ValueParsingService` — Amount parsing:** handles currency symbols, parentheses for negatives `(100.00) = -100.00`, locale-aware separators.

---

## Parsing Reference Specification

### Encoding Handling

- Try UTF-8 strict: `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)`
- On `DecoderFallbackException` → retry as Windows-1252: `Encoding.GetEncoding(1252)`
- Register `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before first use
- XLSX: ClosedXML handles encoding internally
- JSON: `StreamReader` with `detectEncodingFromByteOrderMarks: true`
- PDF: PdfPig extracts Unicode text — no action needed

### Headerless File Fallback

- `HeaderDetectionService` returns `null` → positional: col 0=Date, col 1=Description, col 2=Amount
- Fewer than 3 columns → throw `PARSE_ERROR`

### Row Materialization Failure Policy

- Unparseable date or amount → catch `FormatException` → skip row → add to `skippedReasons`
- Missing description → empty string; do not skip
- Never throw on a single bad row

### `PdfRowFilter` — Exact Token Lists

Applied after `HeaderNormalization.Normalize()`:

| Method | Match type | Tokens |
|--------|------------|--------|
| `LooksLikeHeader` | Exact | `beskrivning`, `referens`, `belopp`, `bokfortsaldo`, `transaktionsdag`, `bokforingsdag`, `valutadag` |
| `LooksLikeNoise` | Prefix | `saldo`, `kontohavare`, `privatkonto`, `transaktioner`, `skapad` |
| `LooksLikeNoise` | Exact | `sek` |

### PDF Strategy Constants and Regex Patterns (Exact)

#### `ColumnLayoutPdfStrategy`

```
RowTolerance=5.0, BookingDateLeft=145, BookingDateRight=205
TransactionDateLeft=205, TransactionDateRight=260, ValueDateLeft=260, ValueDateRight=312
DescriptionLeft=312, DescriptionRight=438, AmountLeft=438, AmountRight=490
Word filter: BoundingBox.Left >= 141
Date validation: ^\d{4}-\d{2}-\d{2}$
Date priority: transactionDate → bookingDate → valueDate

RowTolerance is 5.0 (not 2.5): SEB PDFs render the transaction table inside XObject
forms with independent coordinate systems. Words that belong to the same row but
come from different form elements can have Y positions that differ by up to ~4 pts.
RowTolerance=2.5 causes those words to be bucketed into separate rows, both of
which get dropped (one has no amount, the other no description).

Amount fallback: if JoinWords(AmountLeft, AmountRight) returns empty, scan ALL words
on the row (no X constraint) matching SwedishAmountRegex = ^-?[\d ]+,\d{2}$. Prefer
the first negative token (expense amount); if none, take the first matching token.
Negative-first preference avoids accidentally picking the running balance (positive).

Description fallback: if JoinWords(DescriptionLeft, DescriptionRight) returns empty,
take all words on the row that are neither a date (^\d{4}-\d{2}-\d{2}$) nor a
Swedish amount token, joined with spaces.

Note: ~3% of PDF rows remain unrecoverable (XObject coordinate offsets too severe).
These are always one-off transactions — repeated-pattern analysis is unaffected.
```

#### `SequentialTablePdfStrategy`

```
AnyDateRegex:          \d{4}-\d{2}-\d{2}
SequentialRecordRegex: (?<date>\d{4}-\d{2}-\d{2})(?<description>.*?)(?<transactionId>TXN\d+)(?<type>Debit|Credit)(?<amount>[\+\-]?\d[\d,\.]*)(?<currency>[A-Z]{3})(?<balance>[\+\-]?\d[\d,\.]*)(?=(?:\d{4}-\d{2}-\d{2})|$)
```

#### `RegexRowPdfStrategy`

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

| Code | Condition |
|------|-----------|
| `INVALID_FILE` | File missing, empty, unreadable, or invalid before parsing |
| `UNSUPPORTED_FORMAT` | No parser found for the file extension |
| `PARSE_ERROR` | Parser found but throws, or input shape unsupported |
| `NO_DATA_FOUND` | Preserve existing CSV behavior only — do not extend to other formats |

---

## Temp File Cleanup (Mandatory)

If a temp file is created, it must always be deleted in a `finally` block on all exit paths.

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

CSV, XLSX, and JSON results for semantically equivalent input must be structurally, semantically, and deterministically equivalent.

### PDF Orchestrator Tests (3 Required)

**Precedence test:** PDF matching both `SequentialTablePdfStrategy` and `RegexRowPdfStrategy` → must use SequentialTable rows exclusively.

**Failure test:** PDF with no machine-readable text → must throw `PARSE_ERROR`. No partial result.

**No-mixing test:** multi-page PDF where page 1 matches strategy A and page 2 would only match strategy B → rows from strategy A for all pages.

---

## Deliverables

Return all 17 new component files plus required test files. Only modify the explicitly allowed existing files (controller, `Program.cs`, `.csproj`, test project). No explanations or markdown outside of code files.
