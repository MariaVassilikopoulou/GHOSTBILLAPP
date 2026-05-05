---
name: ghostbill-fullstack-v2
description: Implement or extend Ghostbill end to end from the promptvolume2 fullstack spec. Use when a task spans backend and frontend, especially multi-format transaction ingestion (CSV, XLSX, JSON, PDF), recurring-expense analysis, and the React UI.
---

# Ghostbill Fullstack V2

Use this skill for Ghostbill work that must keep the backend parsing pipeline, analysis behavior, and frontend UI aligned.

## Workflow

1. Read `references/source-prompt.md` in full before editing. It is the authoritative specification.
2. Inventory the workspace and determine whether the backend and frontend baseline already exist.
3. Follow the execution order from the reference prompt. Do not reorder core phases unless the existing codebase makes a step unnecessary.
4. Treat all exact contracts in the reference as fixed: paths, namespaces, API route and DTO shapes, hero copy, thresholds, sort rules, badges, and error codes.
5. Preserve all listed blocked actions, especially any constraint around `CsvParsingService`, pipeline semantics, business logic placement, frontend dependencies, and deterministic behavior.
6. Verify the required backend and frontend builds/tests before finishing.

## Guardrails

- Parsers are translators only; keep business logic in analysis.
- Equivalent transaction data across supported formats must produce equivalent analysis results.
- If the current workspace conflicts with a blocked action in the reference prompt, stop and surface that conflict before changing the restricted area.

## Reference

- `references/source-prompt.md`: full authoritative fullstack prompt
