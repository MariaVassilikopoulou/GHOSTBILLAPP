# Frontend Implementation Prompt — Ghostbill (v2)

## Role
You are a frontend developer implementing the Ghostbill UI in React + TypeScript + Vite + plain CSS. Your task is to build a polished, performant upload-and-results interface that communicates recurring expense insights clearly to the user.

---

## Immutable Constraints — Read Before Anything Else

- **Backend freeze:** no changes to `/services/api.ts`, API contracts, DTOs, endpoints, or request structure
- **No new dependencies:** plain CSS only, no Tailwind, no UI frameworks, no animation libraries
- **No business logic changes:** ghost/regular detection is backend-only and must not be reimplemented or approximated in the frontend
- **Files:** only the files listed in Deliverables may be created or modified

---

## Implementation Priorities
Correctness > performance > visuals > polish
Determinism > creativity
Simplicity > abstraction

---

## Product Context

### What Ghostbill Does
Ghostbill is an expense-awareness tool — not a budgeting app or income tracker. Users upload a bank file. The backend analyzes recurring outgoing charges and classifies them as:
- **Ghosts** — recurring charges with consistent timing and amount, likely forgotten subscriptions
- **Regulars** — recurring charges with expected variation, such as utilities or rent

### User Journey
1. Select or drag in a supported file: `.csv`, `.xlsx`, `.json`, or text-based `.pdf`
2. Wait for analysis
3. Review Ghost and Regular charges in the merchant list
4. Dismiss known ghost charges or download the ghost list
5. Re-upload a new file next month — new ghosts are highlighted automatically
6. Reset and upload another file if needed

### What the UI Must Communicate
- "Ghost" = likely forgotten recurring charge, not fraud
- Ghostbill focuses on **outgoing expenses** — income and credits are excluded
- The `totalTransactionsAnalyzed` count reflects only outgoing transactions
- Unsupported or unparseable files must fail clearly with an actionable message

### Hero Section (Exact Text Required)
- **Headline:** "Spot the charges that quietly keep billing you."
- **Subtitle:** "Upload a bank file to separate likely forgotten subscriptions from the bills that keep coming that you actually expect."
- **Format chips:** visual pill badges below the subtitle — one per format: CSV · XLSX · JSON · PDF

---

## Data Contracts

### Existing Types (Do Not Modify)
`AnalysisResult`, `AnalysisSummary`, `RecurringExpenseGroup`, `Transaction` — all defined in `/services/api.ts`.

### MerchantRowModel
Computed in `useAnalysisMemo`. Extends `RecurringExpenseGroup` with:

| Field | Formula | Used for |
|-------|---------|---------|
| `rank` | 1-based sort position by `totalAmount` desc | Animation delay: `rank × 70ms` |
| `spendShare` | `totalAmount / topMerchant.totalAmount` | Internal; not displayed |
| `annualCost` | `Math.round(averageAmount × (365 / cadenceDays))` | Annual projection on ghost rows |
| `monthsRunning` | `Math.max(1, Math.round((occurrences × cadenceDays) / 30))` | Duration label |
| `priceDrift` | `Math.round(((lastTrend - firstTrend) / firstTrend) × 100)` or `0` if < 2 trend points | Internal; not displayed |
| `isNewGhost` | `classification === "ghost" && daysSince(firstChargeDate) <= 60` | "New · may be a trial" badge |
| `isNewThisUpload` | `classification === "ghost" && prevGhostNames.size > 0 && !prevGhostNames.has(merchant)` | "New this scan" badge |

`useAnalysisMemo` must also expose:
- `annualGhostCost: number` — sum of `annualCost` across all ghost merchants

`useAnalysisMemo` accepts a second parameter: `prevGhostNames: Set<string>` (loaded from localStorage by `usePrevGhosts`).

### Currency and Locale
All monetary values formatted using:
```typescript
Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' })
```
Apply consistently to: stat cards, merchant rows, "already paid", annual projections, ghost callout banner, export CSV, and timeline cards.

---

## Architecture

### State Domains (Three Independent)
```
fileState:     { selectedFile, preview: { name, size, detectedFormat, estimatedRows? }, uploadStatus }
analysisState: { loading, result, error }
requestState:  { requestId: number, controller: AbortController | null }
```

### Request Safety
Latest-request-wins: increment `requestId` per upload, abort previous `AbortController`, ignore responses for any non-current `requestId`.

### Memoization
- `useMemo` for all derived data: merchant rows, stat card values, timeline transactions, `annualGhostCost`
- `React.memo` on the merchant row component
- `useDeferredValue` for the sorted merchants array

### localStorage Keys
| Key | Purpose |
|-----|---------|
| `ghostbill_dismissed` | JSON array of merchant names the user has marked as known |
| `ghostbill_prev_ghosts` | JSON array of ghost merchant names from the previous upload |

---

## UX Specification

### File Upload Zone
- Drag-and-drop overlay on `dragenter`; hover and active visual states
- File validation by extension: `.csv`, `.xlsx`, `.json`, `.pdf`; also accept by MIME type where available
- **File preview** (shown immediately after selection, before upload):
  - File name, file size, detected format label
  - Estimated row count for CSV and JSON only (best-effort is acceptable)
  - **Do not show row count for PDF or XLSX**

### Two Reset Buttons (Both Required)

| Button | Behavior |
|--------|---------|
| **"Reset file"** | Aborts in-flight request. Clears: file, preview, uploadStatus, loading, error. **Preserves** `result`. |
| **"Reset all"** | Same as above, plus clears `result`. Results panel disappears entirely. |

### Loading State
- Skeleton loaders for stat cards and merchant rows
- Do NOT use spinner-only UI

### Empty State
- Text: "No ghosts found yet — upload a bank file to scan for forgotten charges"
- Visual: `GhostIcon` component at 48px, `color: rgba(207, 127, 40, 0.5)`
- Drag zone pulses gently when idle; animation stops on drag or upload

### Error Handling

| Error code | Required behavior |
|------------|------------------|
| `INVALID_FILE` | Toast with clear message |
| `UNSUPPORTED_FORMAT` | Toast that names the supported formats |
| `PARSE_ERROR` | Toast with message + optional details |

- CSS-only toast/snackbar (no library)
- `aria-live="assertive"` region for errors
- User can dismiss; error clears on new upload

### Animations

| Animation | Allowed | Detail |
|-----------|---------|--------|
| Row reveal stagger | Yes | `animationDelay: rank × 70ms` |
| Stats count-up | Yes | RAF or CSS |
| Drag zone idle pulse | Yes | CSS only, stops on drag/upload |
| Animation libraries | No | Plain CSS only |

---

## Ghost Icon Component (`GhostIcon`)
Defined as an inline SVG React component in `App.tsx` — no separate file, no emoji, no external image.
- Shape: classic ghost silhouette — domed head, scalloped wavy base, two white oval eyes
- Uses `currentColor` fill so CSS controls color per context
- Accepts a `size` prop (number, in px)
- Used at: 12px (badges), 14px (ghost section eyebrow), 28px (callout banner), 48px (empty state)

---

## Merchant Row Specification

Each row displays in a two-column grid (merchant info | spend metrics) with a dismiss button on ghost rows:

1. **Badge** — ghost (amber) or regular (green):
   - Ghost: `background: rgba(207,127,40,0.14)`, `color: #7a4200`, `GhostIcon` at 12px inline before label
   - Regular: `background: rgba(30,95,87,0.1)`, `color: #1a5e56`

2. **"New this scan" badge** — ghost rows only, shown when `isNewThisUpload === true`. Indicates this ghost did not appear in the previous upload's ghost list. Style: green tint (`background: rgba(30,95,87,0.09)`, `color: #1a5e56`).

3. **"New · may be a trial" badge** — ghost rows only, shown when `isNewGhost === true` AND `isNewThisUpload === false`. Indicates first charge was within last 60 days.

4. **Merchant name**

5. **Description line:** `Every ~{cadenceDays} days · {occurrences} charges · ~{monthsRunning} months`

6. **"Already paid" display** (every row):
   - Label: "already paid" (small uppercase)
   - Primary: `totalAmount` formatted as SEK
   - Secondary: `averageAmount` formatted as SEK + " per charge"
   - Text aligned right

7. **Annual projection** — ghost rows only: `~{annualCost}/yr` in amber bold (`color: #8a4c00`)

8. **"I know about this" dismiss button** — ghost rows only. Small text button, no border. On click, moves merchant to the "Known charges" section.

---

## Ghost and Regular Section Layout

### Ghost Section
- Eyebrow: `<GhostIcon size={14} /> Ghosts`
- Heading: "Charges you may have forgotten about"
- Intro: "Same amount. Same timing. Do you still need these?"
- Left border accent: `border-left: 3px solid rgba(207,127,40,0.4)`

### Ghost Section Header Actions
The ghost section header contains two elements on the right side:
1. **"Download list" button** — downloads a CSV of active (non-dismissed) ghost merchants with: Merchant, Per charge (SEK), Times charged, Already paid (SEK), Est. annual (SEK). Disabled when all ghosts are dismissed. Implemented as a client-side Blob download — no backend call.
2. **Count badge** — total ghost count (including dismissed)

### Ghost Callout Banner (shown above ghost list when active ghosts exist)
- Layout: `<GhostIcon size={28} />` (amber) + text in a flex row
- Text: `{N} forgotten charge(s) quietly costing you ~{annualGhostCost} per year`
- `N` = count of active (non-dismissed) ghosts
- Style: warm amber gradient background, subtle amber border, rounded corners

### Known Charges Section (within ghost panel)
Shown below the ghost merchant list when at least one ghost has been dismissed.
- Layout: `{N} marked as known` label on the left; per-merchant "Undo" buttons on the right
- Background: subtle neutral tint
- On "Undo" click: merchant returns to the active ghost list
- State persists in localStorage (`ghostbill_dismissed`) across reloads and re-uploads

### Regular Section
- Heading: "Expected bills that keep coming"
- Intro: "These look intentional — bills you likely recognise."

---

## Summary Panel — 3 Stat Cards (All Required)

| Card label | Primary value | Subtext | Additional |
|-----------|--------------|---------|-----------|
| "Likely forgotten" | `totalGhostSpend` (monthly) | "Estimated monthly ghost cost" | `~{annualGhostCost}/year` in amber bold (when > 0) |
| "Found in your file" | `totalGhostSpend + totalRegularSpend` | "Total across all repeat charges in this file" | — |
| "Transactions checked" | `totalTransactionsAnalyzed` | `"{totalRecurringCharges} repeat patterns identified"` | — |

Skeleton placeholders for all three cards during loading.

---

## Timeline Panel — "Recent Recurring Charges" (Required)

Rendered below the merchant list whenever `result` is present.

- **Heading:** "Recent recurring charges"
- **Content:** the 8 most recent transactions from all recurring groups (ghosts + regulars combined), sorted by `transaction.date` descending
- **Each card:** merchant name · amount in SEK (absolute value) · date as "MMM D"
- **Layout:** horizontal scrollable card grid
- Do not render this panel when there are no results

---

## Cross-Upload Ghost Comparison

After each successful analysis, save the current ghost merchant names to localStorage (`ghostbill_prev_ghosts`).

On the next upload, any ghost merchant NOT found in the stored previous list receives the `isNewThisUpload = true` flag.

Rules:
- Save ghost names AFTER computing `isNewThisUpload` (not before)
- Only show "New this scan" badge when there IS a previous upload stored (`prevNames.size > 0`)
- First-time users see no "New this scan" badges

Implemented in: `usePrevGhosts.ts`. Used via `useEffect` in `App.tsx` watching `analysisState.result`.

---

## Performance
- Handle ~10k rows with no noticeable lag
- Stable component keys, no layout thrashing, minimal rerenders

## Accessibility
- `aria-live="polite"` for loading and results changes
- `aria-live="assertive"` for errors; clear after user dismisses
- Keyboard-accessible upload (tab to button, Enter/Space activates)
- Visible focus states on all interactive elements

## Responsiveness
- Mobile: tap-to-upload, single-column layout
- Desktop: drag-and-drop functional, hover states visible

---

## Dev Mode
Activated by `?dev=1` in URL. Skips API calls, uses inline mock `AnalysisResult`.

Mock data must include:
- **Ghost:** merchant "Netflix", ~149 SEK, every ~30 days, 6+ occurrences, classification "ghost"
- **Regular:** merchant "City Utilities", variable amounts (~300–450 SEK), ~monthly, classification "regular"

A **"Dev" pill badge** must appear in the merchant panel heading when dev mode is active.

---

## Deliverables

Return **only** these files, fully functional and compilable with no TypeScript errors:

1. `frontend/ghostbill-ui/src/App.tsx`
2. `frontend/ghostbill-ui/src/App.css`
3. `frontend/ghostbill-ui/src/useFileUpload.ts`
4. `frontend/ghostbill-ui/src/useAnalysisMemo.ts`
5. `frontend/ghostbill-ui/src/useDismissed.ts`
6. `frontend/ghostbill-ui/src/usePrevGhosts.ts`

Plus: `CHANGELOG.md` (max 5 lines).

**Rules:**
- No explanations or markdown outside of the files above
- No partial code, placeholder comments, or TODOs
- Must compile without TypeScript errors or console warnings
- Preserve all existing backend-freeze constraints

---

## Acceptance Checklist

### Upload
- [ ] Hero section shows exact headline, subtitle, and format chips
- [ ] Drag CSV/XLSX/JSON/PDF → file preview shown immediately
- [ ] PDF and XLSX previews show no row count estimate
- [ ] CSV and JSON previews show best-effort row count
- [ ] "Reset file" clears upload state, results remain visible
- [ ] "Reset all" clears upload state AND results panel
- [ ] Multiple sequential uploads produce no stale data

### Analysis and Results
- [ ] Upload → skeleton loaders → animated results
- [ ] 3 stat cards present with correct labels and values in SEK
- [ ] "Likely forgotten" card shows both monthly and annual ghost cost
- [ ] Merchant rows staggered by spend rank (70ms × rank delay)
- [ ] "Already paid" total + per-charge average shown on every merchant row
- [ ] Annual `~X/yr` projection shown on ghost rows in amber
- [ ] Ghost section: eyebrow with GhostIcon, heading, intro, left-border accent
- [ ] Regular section heading: "Expected bills that keep coming"
- [ ] Ghost callout banner shows count of active ghosts + annual cost in SEK
- [ ] Ghost badges amber-tinted, regular badges green-tinted
- [ ] Timeline panel appears below merchant list with up to 8 cards

### Export
- [ ] "Download list" button appears in ghost section header when ghosts exist
- [ ] Clicking it downloads a CSV with columns: Merchant, Per charge, Times charged, Already paid, Est. annual
- [ ] Button is disabled when all ghosts are dismissed
- [ ] No backend call made for export

### Dismiss
- [ ] Each ghost row shows "I know about this" button
- [ ] Clicking dismiss moves merchant to "Known charges" section below the ghost list
- [ ] "Known charges" section shows count + per-merchant "Undo" buttons
- [ ] Clicking "Undo" returns merchant to active ghost list
- [ ] Dismissed state persists across page reloads
- [ ] Dismissed state persists across re-uploads of the same file

### Cross-Upload Comparison
- [ ] First upload: no "New this scan" badges shown
- [ ] Second upload: ghosts not present in the first result show "New this scan" badge
- [ ] Ghosts present in both uploads show no "New this scan" badge
- [ ] "New · may be a trial" badge shown only when `isNewGhost && !isNewThisUpload`

### States
- [ ] Empty state: GhostIcon at 48px, faded amber, correct text
- [ ] Loading: skeleton loaders (not spinner-only)
- [ ] `INVALID_FILE`, `UNSUPPORTED_FORMAT`, `PARSE_ERROR` each show accessible toast
- [ ] Unsupported format error names the supported formats

### Other
- [ ] Works on mobile (tap-to-upload, vertical layout)
- [ ] Works on desktop (drag-and-drop, hover states)
- [ ] Dev mode (`?dev=1`): Netflix ghost + City Utilities regular, "Dev" badge visible
- [ ] All monetary values formatted in SEK
- [ ] No TypeScript errors, no console warnings
