# The Workshop in the GitHub Copilot app

The app-first path: install the plugin, switch into the Workshop TA, open a desk,
and bring up the 🪨 Cairn dashboard, all from inside the GitHub Copilot app. No
terminal required to run the room.

> Prefer the command line? The same four steps in CLI form live in
> [`quickstart.md`](quickstart.md). Looking for the standalone Blazor operator
> dashboard (`Start.bat`) instead of the in-app canvas? That's
> [`getting-started.md`](getting-started.md). New to the whole idea of desks?
> [`desk-vs-subagent.md`](desk-vs-subagent.md) is the one-page concept.

## What you need

- The **GitHub Copilot app**, signed in.
- A folder to be your **workshop**: any git repo or working directory. Desks and
  their memory live under `desks/` inside it.

That's it. The Workshop is already in `awesome-copilot`, GitHub's default plugin
marketplace, so there's nothing to add to your sources first.

## 1. Install the plugin

In the app's chat box, run the plugin install as a slash command:

```
/plugin install the-workshop@awesome-copilot
```

You get the **Workshop TA** (the room coordinator) plus five desk skills:
`desk-open`, `desk-journal`, `signal-write`, `bench-read`, and `workshop-create`.

## 2. Switch into the Workshop TA

This is the step most people miss. Installing the plugin *adds* the TA agent, but
it does not switch you into it. You are still talking to a plain assistant until
you pick the coordinator.

When you start a session, choose **Workshop TA** from the agent selector. From
that point on you are talking to the room coordinator: the agent that sees every
desk and routes the work, so you direct the team instead of relaying between them.

## 3. Open a desk

Tell the TA what you are working on and ask it to open a desk. For example:

> open a desk called `api-refactor` to work on the checkout service

The TA sets up the desk's folder, its journal (persistent memory that carries
across sessions), and its signal channel (structured state: blocked, done,
hands-up). Open as many desks as the work needs. They share the one workspace and
they are allowed to disagree with each other, which is often the most useful thing
that happens in the room.

## 4. Bring up the 🪨 Cairn dashboard

Cairn is a live canvas that reads every desk's signals and shows the whole room at
a glance: who is blocked, who is done, who has hands up. It is a companion canvas
extension, separate from the plugin, and it does not auto-load on install, so you
bring it up once. Just ask the TA:

> run cairn

It installs the `signals-dashboard` canvas and opens it in the canvas panel. From
then on it updates live as desks emit signals.

Each desk card on the board has an **open** button that launches a Copilot CLI
right in that desk's folder: an in-place session inside your workshop repo, so
every desk stays in the one repo you coordinate through (its journal, its
`.signals`, and the shared board) rather than a separate checkout elsewhere.

If you would rather wire the canvas in by hand, copy
`.github/extensions/signals-dashboard/` from this repo into your own workshop's
`.github/extensions/` folder. It then auto-loads for every session in that
workshop.

## 5. Run the room

With desks running and Cairn up, you have a room instead of a chat tab:

- The board tells you what actually needs you. A weak signal or an **⚡
  escalation** rises into the **TA** strip so you can make the call and dismiss it.
- Desks are long-running in **state**, not in **runtime**. Each session is
  independent; the journal is the bridge that carries the work forward.
- You set direction and settle what the room cannot settle on its own. That is the
  whole job: direct a team, do not relay between agents.

## Where to go next

- [`quickstart.md`](quickstart.md) — the same flow from the command line.
- [`getting-started.md`](getting-started.md) — the standalone Blazor operator
  dashboard (`Start.bat`), which reads your live desks from session-state.
- [`why-use-this.md`](why-use-this.md) — why a room of desks beats one long chat.
- [`desk-vs-subagent.md`](desk-vs-subagent.md) — a desk has its own frame; a
  sub-agent inherits yours.

---

_App screenshots to come. The flow above is verified against the plugin as shipped
in `awesome-copilot`; the in-app chrome (agent selector, canvas panel) may move as
the app evolves._
