---
name: ghostbill-backend-v2
description: Extend the Ghostbill ASP.NET Core backend using the promptvolume2 backend spec. Use when adding or maintaining multi-format transaction parsing, parser resolution, recurring-expense analysis behavior, and backend verification without changing frozen CSV behavior or API contracts.
---

# Ghostbill Backend V2

Use this skill for Ghostbill backend work that must preserve legacy CSV behavior while adding or maintaining XLSX, JSON, and PDF support in the existing pipeline.

## Workflow

1. Read `references/source-prompt.md` in full before editing. It is the authoritative specification.
2. Detect whether a legacy `CsvParsingService` already exists. If it does, treat it as frozen. If it does not, create the baseline exactly as required before any non-CSV parser work.
3. Implement in the sequence defined by the reference prompt: contracts and analysis first, then CSV validation, then XLSX/JSON, then PDF strategies, then controller and DI wiring.
4. Treat all exact backend contracts as fixed: file paths, namespaces, interfaces, parser precedence, alias lists, regexes, constants, error codes, and output ordering.
5. Keep parsers translation-only and format-agnostic from the analysis layer.
6. Run the required build and test verification before finishing.

## Guardrails

- Do not change `CsvParsingService` internals, signature, or observable output when a legacy implementation exists.
- Do not alter the API route, request fields, DTO shapes, pipeline semantics, or analysis thresholds unless explicitly instructed outside this skill.
- Do not add OCR, nondeterministic behavior, or business logic inside parsers or shared parsing helpers.

## Reference

- `references/source-prompt.md`: full authoritative backend prompt
