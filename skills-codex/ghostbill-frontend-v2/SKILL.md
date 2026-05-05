---
name: ghostbill-frontend-v2
description: Build or extend the Ghostbill React frontend from the promptvolume2 frontend spec. Use when implementing the upload flow, result presentation, localStorage-backed ghost tracking, timeline/stat UI, and plain-CSS interactions while preserving the frozen backend API contract.
---

# Ghostbill Frontend V2

Use this skill for Ghostbill UI work in React, TypeScript, Vite, and plain CSS when the backend API contract is frozen and the promptvolume2 frontend behavior must be followed closely.

## Workflow

1. Read `references/source-prompt.md` in full before editing. It is the authoritative specification.
2. Treat all exact UX contracts as fixed: hero text, supported formats, state domains, localStorage keys, badge rules, reset behavior, timeline rules, required deliverables, and accessibility expectations.
3. Preserve the backend freeze in the reference prompt. Do not change `/services/api.ts`, API contracts, DTOs, endpoints, or request structure.
4. Keep the implementation dependency-free beyond the existing frontend stack: plain CSS only, no UI frameworks, no animation libraries, no reimplementation of backend business logic.
5. Ensure the upload flow uses latest-request-wins request handling and that derived display data stays in memoized frontend helpers as specified.
6. Run the required frontend verification before finishing.

## Guardrails

- Keep all money formatting in SEK using the exact locale/currency formatter from the reference prompt.
- Do not approximate ghost/regular classification in the frontend.
- When the prompt restricts modifications to an exact file list, treat that list as binding unless the user overrides it.

## Reference

- `references/source-prompt.md`: full authoritative frontend prompt
