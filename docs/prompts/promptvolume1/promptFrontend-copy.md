# Frontend Implementation Prompt — Ghostbill

## Role
You are a frontend developer implementing the Ghostbill UI in React + TypeScript + Vite + plain CSS. Your task is to build a polished, performant upload-and-results interface that communicates repeated expense insights clearly to the user.

---

## Immutable Constraints — Read Before Anything Else

- **Backend freeze:** no changes to `/services/api.ts`, API contracts, DTOs, endpoints, or request structure
- **No new dependencies:** plain CSS only, no Tailwind, no UI frameworks, no animation libraries
- **No business logic changes:** ghost/regular detection is backend-only and must not be reimplemented or approximated in the frontend
- **Files:** only the four files listed in Deliverables may be created or modified

---

## Implementation Priorities
Correctness > performance > visuals > polish
Determinism > creativity
Simplicity > abstraction

---

## Product Context

### What Ghostbill Does
Ghostbill is an expense-awareness tool — not a budgeting app or income tracker. Users upload a bank export file. The backend analyzes repeated outgoing charges and classifies them as:
- **Ghosts** — repeated charges with consistent timing and amount, likely forgotten subscriptions
- **Regulars** — repeated charges with expected variation, such as utilities or rent

### User Journey
1. Select or drag in a supported file: `.csv`, `.xlsx`, `.json`, or text-based `.pdf`
2. Wait for analysis
3. Review Ghost and Regular charges in the merchant list
4. Browse recent individual charges in the timeline panel
5. Reset and upload another file if needed

### What the UI Must Communicate
- "Ghost" = likely forgotten repeated charge, not fraud
- Ghostbill focuses on **outgoing expenses** — income and credits are excluded
- The `totalTransactionsAnalyzed` count reflects only outgoing transactions; it will be lower than the file's total row count
- Unsupported or unparseable files must fail clearly with an actionable message

### Hero Section (Exact Text Required)
- **Headline:** "Spot the charges that quietly keep billing you."
- **Subtitle:** "Upload a bank file to separate likely forgotten subscriptions from the bills that keep coming that you actually expect."
- **Format chips:** visual pill badges below the subtitle — one per format: CSV · XLSX · JSON · PDF

---

## Data Contracts

### Existing Types (Do Not Modify)
`AnalysisResult`, `AnalysisSummary`, `RecurringExpenseGroup`, `Transaction` — all defined in `/services/api.ts`. Do not add fields or change shapes.

### TrendPoint
Each `RecurringExpenseGroup` carries `trend: TrendPoint[]` used to render sparklines:

```typescript
type TrendPoint = {
  label: string;   // "YYYY-MM" — one entry per calendar month
  amount: number;  // sum of transaction amounts for that month
}
// Ordered chronologically. Do not reorder or invent data.
```

### MerchantRowModel
Computed in `useAnalysisMemo`. Extends `RecurringExpenseGroup` with:

| Field | Formula | Used for |
|-------|---------|---------|
| `rank` | 1-based sort position by `totalAmount` desc | Animation delay: `rank × 70ms` |
| `spendShare` | `totalAmount / topMerchant.totalAmount` | Spend bar width (relative to top spender, not combined total) |
| `annualCost` | `Math.round(averageAmount × (365 / cadenceDays))` | Annual projection on ghost rows |
| `monthsRunning` | `Math.max(1, Math.round((occurrences × cadenceDays) / 30))` | Duration label |
| `priceDrift` | `Math.round(((lastTrend - firstTrend) / firstTrend) × 100)` or `0` if < 2 trend points | Price change % |
| `isNewGhost` | `classification === "ghost" && daysSince(firstChargeDate) <= 60` | "New · may be a trial" badge |

`useAnalysisMemo` must also expose:
- `annualGhostCost: number` — sum of `annualCost` across all ghost merchants

### Currency and Locale
All monetary values formatted using:
```typescript
Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' })
```
Apply consistently to: stat cards, merchant rows, "already paid", annual projections, ghost callout banner, and timeline cards.

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
- `useDeferredValue` for the sorted merchants array to keep interactions responsive

---

## UX Specification

### File Upload Zone
- Drag-and-drop overlay on `dragenter`; hover and active visual states
- File validation by extension: `.csv`, `.xlsx`, `.json`, `.pdf`; also accept by MIME type where available
- **File preview** (shown immediately after selection, before upload):
  - File name, file size, detected format label
  - Estimated row count for CSV and JSON only (best-effort is acceptable)
  - **Do not show row count for PDF or XLSX** — cannot be reliably estimated client-side before upload

### Two Reset Buttons (Both Required)

| Button | Behavior |
|--------|---------|
| **"Reset file"** | Aborts in-flight request. Clears: file, preview, uploadStatus, loading, error. **Preserves** `result` — analysis stays visible. |
| **"Reset all"** | Same as above, plus clears `result`. Results panel disappears entirely. |

### Loading State
- Skeleton loaders for stat cards and merchant rows
- Do NOT use spinner-only UI

### Empty State
- Text: "No ghosts found yet — upload a statement to scan for forgotten charges"
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
| Stats count-up | Yes | RAF or CSS |
| Row reveal stagger | Yes | `animationDelay: rank × 70ms` |
| Gradient spend bars | Yes | CSS gradient |
| Drag zone idle pulse | Yes | CSS only, stops on drag/upload |
| Animation libraries | No | Plain CSS only |
| Complex motion systems | No | — |

---

## Ghost Icon Component (`GhostIcon`)
Defined as an inline SVG React component in `App.tsx` — no separate file, no emoji, no external image.
- Shape: classic ghost silhouette — domed head, scalloped wavy base, two white oval eyes
- Uses `currentColor` fill so CSS controls color per context
- Accepts a `size` prop (number, in px)
- Used at: 12px (badges), 14px (ghost section eyebrow), 28px (callout banner), 48px (empty state)

---

## Merchant Row Specification

Each row displays:

1. **Badge** — ghost (amber) or regular (green):
   - Ghost: `background: rgba(207,127,40,0.14)`, `color: #7a4200`, `GhostIcon` at 12px inline before label
   - Regular: `background: rgba(30,95,87,0.1)`, `color: #1a5e56`

2. **"New · may be a trial" badge** — ghost rows only, shown when `isNewGhost === true`. Tells the user this charge started recently and may be a trial they forgot to cancel.

3. **Merchant name** and **average amount**

4. **Spend bar** — gradient, width = `spendShare × 100%` (relative to top merchant, not combined total)

5. **Sparkline** — SVG only, no libraries. Data source: `group.trend` (one point per month). Lightweight shape reflecting direction; no axis labels required.

6. **Description line:** `Every ~{cadenceDays} days · {occurrences} charges · ~{monthsRunning} months`

7. **"Already paid" display** (every row):
   - Label: "already paid"
   - Primary: `totalAmount` formatted as SEK
   - Secondary: `averageAmount` formatted as SEK + " per charge"

8. **Annual projection** — ghost rows only: `~{annualCost}/yr` in amber bold (`color: #8a4c00`)

---

## Ghost and Regular Section Layout

### Ghost Section
- Eyebrow: `<GhostIcon size={14} /> Ghosts`
- Heading: "Charges you may have forgotten about"
- Intro: "Same amount. Same timing. Do you still need these?"
- Left border accent: `border-left: 3px solid rgba(207,127,40,0.4)`

### Ghost Callout Banner (shown above ghost list when ghosts exist)
- Layout: `<GhostIcon size={28} color="#cf7f28" />` + text in a flex row
- Text: `{N} forgotten charge(s) quietly costing you ~{annualGhostCost} per year`
- Style: warm amber gradient background, subtle amber border, rounded corners

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

- **Heading:** "Recent repeated charges"
- **Content:** the 8 most recent transactions from all repeated groups (ghosts + regulars combined), sorted by `transaction.date` descending
- **Each card:** merchant name · amount in SEK (absolute value) · date as "MMM D" (e.g. "Jan 5") in user's local timezone
- **Layout:** horizontal scrollable card grid
- Do not render this panel when there are no results

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

Return **only** these four files, fully functional and compilable with no TypeScript errors:

1. `frontend/ghostbill-ui/src/App.tsx`
2. `frontend/ghostbill-ui/src/App.css`
3. `frontend/ghostbill-ui/src/useFileUpload.ts`
4. `frontend/ghostbill-ui/src/useAnalysisMemo.ts`

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
- [ ] Spend bar width proportional to top merchant, not combined total
- [ ] Sparkline renders from `trend` monthly data
- [ ] "Already paid" total + per-charge average shown on every merchant row
- [ ] Annual `~X/yr` projection shown on ghost rows in amber
- [ ] Ghost rows with `isNewGhost = true` show "New · may be a trial" badge
- [ ] Ghost section: eyebrow, heading, intro, left-border accent
- [ ] Regular section heading: "Expected bills that keep coming"
- [ ] Ghost callout banner shows count + annual cost in SEK
- [ ] Ghost badges amber-tinted, regular badges green-tinted
- [ ] Timeline panel appears below merchant list with up to 8 cards

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
