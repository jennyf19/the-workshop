# 🪨 Cairn — Signals Dashboard

A live GitHub Copilot CLI **canvas extension** that shows the pulse of every
desk in your Workshop. It reads the agent signals your desks emit and renders
them as a compact, always-current dashboard in a side panel — so you can direct
the work instead of polling each desk by hand.

It replaces the old Blazor **WorkshopRoom** dashboard (`src/WorkshopRoom/`) with
a native canvas that runs inside GHCP, with no separate web app to launch.

## What it is

Each desk in the Workshop leaves signals behind — small stones on the trail —
by writing JSON files into its `.signals/` folder. Cairn scans those folders,
picks the most recent signal per desk, and renders:

- **Score bars** for the desk's self-assessment (intent, confidence, accuracy,
  completeness).
- **Patterns** the desk reported: what worked ✓, what was hard △, and skill
  gaps ✗.
- **Escalations** — desks that raised their hand, with what they're blocked on
  and their recommendation, pinned to the top and pulsing red.

## How to open it

The dashboard is registered as the **🪨 Cairn** canvas (`signals-dashboard`).
Ask Copilot to open it and pass your workshop root as `workshopDir`:

> Open the 🪨 Cairn canvas with `workshopDir` set to the folder that contains
> my `desks/` directory.

`workshopDir` must be the **absolute path to the workshop root** — the folder
that holds `desks/` (and optionally `classroom/`). If omitted, it falls back to
the current working directory.

## Features

- **Signal scanning** — walks `desks/*/.signals/` and `classroom/*/.signals/`,
  reading the newest `*.json` per desk (mirrors `SignalReader.cs`).
- **Score bars** — color-coded intent / confidence / accuracy / completeness,
  scored out of 5.
- **Escalation alerts** — escalation signals sort to the very top, render with a
  pulsing red border, and surface the blocker + recommendation.
- **Active desks first** — sorted escalations → recent signals → desks with no
  signal yet, then by recency.
- **Stash / restore** — pause a workstream by stashing its desk. Stashed desks
  drop off the active view and auto-expire after a **48-hour TTL**; restore any
  time before then. Stash state lives in `.desk-stash.json` at the workshop
  root.
- **Auto-refresh** — the panel refreshes every 5 seconds using a background
  fetch (no full page reload), so scores and escalations stay current smoothly.
- **Summary bar** — desk count, how many are reporting vs. awaiting, an
  escalation badge, and average scores across the room.

## Agent actions

The canvas also exposes actions Copilot can invoke directly:

- `refresh` — force a rescan and return current signal data as JSON.
- `stash` — stash a desk by `deskName`.
- `restore` — restore a stashed desk by `deskName`.

## Signal shape

Cairn reads the agent-signals protocol used across the Workshop:

```json
{
  "signal_type": "execution",
  "agent_name": "desk-name",
  "self_assessment": { "intent": 5, "confidence": 4, "accuracy": 4, "completeness": 3 },
  "patterns": { "what_worked": "...", "what_was_hard": "...", "skill_gap": "..." },
  "escalation": { "reason": "...", "blocked_on": "...", "recommendation": "..." }
}
```

`escalation` is only present on `signal_type: "escalation"` signals.

## Replaces the Blazor WorkshopRoom

This canvas supersedes the standalone Blazor dashboard in `src/WorkshopRoom/`.
The data is the truth and the UI is just a view — Cairn renders the same signal
data natively inside GHCP, so there's no separate server to run.
