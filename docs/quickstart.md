# Quickstart

Get a room of long-running AI agents running in about five minutes: install the
plugin, run as the TA, open a desk, and bring up the 🪨 Cairn dashboard.

## Prerequisites

- The **GitHub Copilot app** or **Copilot CLI** (`copilot`).
- A folder to be your workshop — any git repo or working directory. Desks and their
  memory live under `desks/` inside it.

## 1. Install the plugin

The Workshop is in `awesome-copilot`, GitHub's default plugin marketplace, so
there's nothing to add first:

```
copilot plugin install the-workshop@awesome-copilot
```

In the GitHub Copilot app, use `/plugin install the-workshop@awesome-copilot`.

*(Claude Code: `/plugin marketplace add jennyf19/the-workshop` then
`/plugin install workshop@the-workshop`.)*

## 2. Run as the Workshop TA

**This is the step people miss.** Installing the plugin gives you the TA agent — it
doesn't switch you into it. Start your session as the coordinator:

```
copilot --agent the-workshop:workshop-ta
```

In the GitHub Copilot app, pick **Workshop TA** from the agent selector when you
start a session.

You're now talking to the room coordinator, not a plain assistant.

## 3. Open a desk

Tell the TA what you're working on and ask it to open a desk — for example:

> open a desk called `api-refactor` to work on the checkout service

The TA sets up the desk's folder, journal (persistent memory), and signal channel.
Open as many desks as the work needs; they share the workspace and can disagree with
each other.

## 4. Bring up the 🪨 Cairn dashboard

Cairn is a live canvas showing every desk's signals — who's blocked, who's done, who
has hands up. It's a companion canvas extension. Just ask the TA:

> run cairn

It installs the `signals-dashboard` canvas and opens it. From then on it updates
live as desks emit signals.

*(Manual alternative: copy `.github/extensions/signals-dashboard/` from this repo
into your workshop's `.github/extensions/` folder; it auto-loads for every session
there.)*

## What you get

- **Desks** — long-running agents, each with its own memory and history.
- **Journals** — persistent memory that carries across sessions; the bridge between
  independent sessions.
- **Signals** — structured state (blocked, done, hands-up, checkpoint) the dashboard
  reads.
- **The TA** — sees the whole room and routes work, so you direct instead of relay.

Desks are long-running in *state*, not in *runtime*: each session is independent, and
the journal is what carries the work forward.
