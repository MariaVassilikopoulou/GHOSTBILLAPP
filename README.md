# GhostBill

Upload a bank file and spot the charges quietly billing you month after month — forgotten subscriptions, unused memberships, and recurring bills you never think about.

**[ghostbillapp.vercel.app](https://ghostbillapp.vercel.app)**

---

## What It Does

- Upload a bank file (CSV, Excel, PDF, or JSON)
- The app finds all repeated charges and classifies them:
  - **Ghosts** — same amount, same timing — likely forgotten subscriptions
  - **Regulars** — expected recurring bills like utilities or rent
- See how much each ghost costs you annually
- Dismiss charges you already know about — state persists across reloads
- Download your ghost list as a CSV
- Upload again next month — new ghosts are highlighted automatically

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18 + TypeScript (Vite) |
| Backend | C# / .NET 10 (REST API) |
| Hosting | Vercel (frontend) · Railway (backend) |
| File Support | CSV, XLSX, JSON, PDF |

---

## How I Built It

This project was built through **vibe coding** — collaborating with an AI assistant to write, debug, and ship code through conversation.

**[Claude Code](https://claude.ai/code) by Anthropic** — an AI coding assistant running directly in the terminal. I described what I wanted in plain English; Claude generated, refactored, and fixed code in real time across the full stack.

### My Role

- Defined the product idea and all feature requirements
- Reviewed every piece of AI-generated code for correctness and quality
- Made all architectural decisions — project structure, API design, deployment strategy
- Tested the app end-to-end and steered the AI when output drifted from intent

### Why This Approach Matters

Vibe coding is a real and emerging skill in software development. It demands knowing *what* to build and *why*, writing precise prompts, reading code critically, and making sound decisions under uncertainty. This project demonstrates that full workflow — from idea to deployed product.

---

## Project Structure

```
GHOSTBILLAPP/
├── frontend/   # React UI — drag-drop upload, animated results
├── backend/    # .NET API — file parsing, recurring expense detection
├── data/       # Sample bank files for testing
└── docs/       # Prompts and project documentation
```
