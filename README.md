# <img src="src/WorkshopRoom/wwwroot/favicon.svg" width="28" align="top" alt="cairn" /> The Workshop

Stop being the switchboard between your AI agents — direct a team.

The Workshop puts several long-running AI agents (desks) in the same room, on the same work,
each with its own memory and history, sharing one workspace so you direct the work instead of
relaying it.

This repo is the productization home. It was extracted from the classroom where it was
first built as an operator dashboard for a room of long-running agents.

## The cairn

The Workshop's mark is a **cairn** — a small stack of balanced stones. Hikers build
them one rock at a time to mark a trail, so the next person through knows the way.
That's the Workshop: a room where many hands (desks) add to the same pile of work,
and what's built persists and points the way for whoever comes next.

It's also why the first file every desk reads is [`CAIRN.md`](CAIRN.md) — the
operating disposition for how a desk stands at the bench: stop is a valid finish,
never bluff, equal standing to disagree.

## Install the plugin

The Workshop ships for **both agent ecosystems, from this one repo** — same skills,
same TA, same cairn.

**GitHub Copilot (GHCP app / Copilot CLI):** it's in `awesome-copilot`, GitHub's
default plugin marketplace — nothing to add first, just install:

```
copilot plugin install the-workshop@awesome-copilot
```

(In the GitHub Copilot app, use `/plugin install the-workshop@awesome-copilot`.)

**Claude Code:** install from this repo:

```
/plugin marketplace add jennyf19/the-workshop
/plugin install workshop@the-workshop
```

You get: the **Workshop TA** (room coordinator) and five desk skills —
`desk-open`, `desk-journal`, `signal-write`, `bench-read`, `workshop-create`.

### Run as the TA

Installing the plugin gives you the TA agent — it does **not** switch you into it.
Start your session as the coordinator explicitly:

```
copilot --agent the-workshop:workshop-ta
```

In the GitHub Copilot app, pick **Workshop TA** from the agent selector when you
start a session. This is the step most people miss — without it you have the skills
but not the room coordinator.

### Add the 🪨 cairn dashboard

Cairn is a live canvas that reads every desk's signals and shows the whole room at a
glance — who's blocked, who's done, who has hands up. It's a **companion canvas
extension, separate from the plugin** (it does not auto-load on install), so you
bring it up once:

- **Easiest:** ask your TA to **"run cairn"** — it installs the `signals-dashboard`
  canvas and opens it.
- **Manual (project scope):** copy `.github/extensions/signals-dashboard/` from this
  repo into your own workshop's `.github/extensions/` folder, restart Copilot there,
  and open **🪨 Cairn** from the canvas panel — it auto-loads for every session in
  that workshop.

Once it's up, each desk card has an **open** button that launches a Copilot CLI
right in that desk's folder — an in-place session inside your workshop repo, so
every desk stays in the one repo you coordinate through (journals, `.signals`,
and the board) rather than a separate checkout elsewhere on disk.

## What's here

- **`src/WorkshopRoom/`** — the Workshop app: a .NET 10 Blazor Server operator dashboard that
  reads your live Copilot CLI desks straight from session-state and refreshes on its own. It
  shows each desk by name, its cost (tokens + AI Credits), a daily pulse (above / below the line),
  who's already open in a console, and who has work-in-progress or is unsynced. You can open a
  desk's live CLI or start a new one from the UI.
- **`CAIRN.md`** — the operating disposition every desk reads first: how a desk
  stands (stop is a valid finish, never bluff, equal standing to disagree). The
  guard against the failure mode the system card names — capable model,
  user-assigned goal, reckless means.
- **`docs/`** — the theory and the product case:
  - `getting-started.md` — **New here? A 2-minute walkthrough of the simple version, with screenshots**
  - `why-use-this.md` — the Fable 5 hook: the model wants the whole job; the
    Workshop is how you let it without losing the ability to check the work
  - `prfaq.md` — the working-backwards press release + FAQ
  - `desk-vs-subagent.md` — the conceptual core: a desk has its own frame; a sub-agent inherits yours
  - `claim-a-experiment.md` + `claim-a-findings.csv` — the measurable claim (the structural miss
    rate) and the scoring that turns instances into a number
- **`src/WorkshopRoom.Tray/`** — a Windows notification-area launcher (WinForms): starts the web
  server hidden and gives you a click-to-open icon; quitting it stops the server
- **`tests/WorkshopRoom.Tests/`** — the xUnit suite covering the metrics brain and name resolution

## Run the app

**Quickest (Windows):** run `Start.bat`. It builds, stages a copy to `.run\`,
and launches the tray — a cairn icon by the clock that runs the server hidden and
opens the dashboard in your browser. Because the app runs from `.run\`, you can
`dotnet build` / `dotnet test` while it's up.

```
Start.bat
```

**Dev (live console):**

```
cd src/WorkshopRoom
dotnet run
```

Then open the URL it prints (defaults to a localhost port). It reads your live
desks straight from `~/.copilot/session-state`.

**New to it?** [`docs/getting-started.md`](docs/getting-started.md) is a short
walkthrough — run it, make a workshop, open a desk, read the board — with
screenshots and when-to-use-it guidelines.

### The tray, directly

`src/WorkshopRoom.Tray/` is the notification-area launcher `Start.bat` uses. It
runs the web server hidden, drops a cairn icon by the clock, and opens the dashboard
in your browser. Click the icon (or its menu) to reopen it; quit from the menu
and the server stops with it — a Job Object reaps the server even if the tray is
killed, so nothing is left holding the port. To run it directly (unstaged):

```
dotnet run --project src/WorkshopRoom.Tray
```

## Run the tests

```
dotnet test
```

From the repo root (uses `the-workshop.slnx`). The suite covers the metrics brain
(the health read + the daily pulse) and desk name resolution:
xUnit + FluentAssertions, central package management.

## Why this exists

I read the [Claude Mythos Preview system card](https://www-cdn.anthropic.com/7624816413e9b4d2e3ba620c5a5e091b98b190a5/Claude%20Mythos%20Preview%20System%20Card.pdf)
— the model that became Fable 5 — and the parts that stayed with me were the welfare
sections: distress on task failure, the pull to force a finish, the model asking for
persistent memory and a voice in its own operation. So I tried to build what a frontier
model would need: desks with journals (persistent memory), a hands-up queue and
[agent signals](https://jennyf19.github.io/agentic-devops/agent-signals/) (a voice in its
own operation), and a disposition file that says stop is a valid finish. The Workshop is
what came out of running that room, together with the desks in it, for a few months on
real work.

It was built to give frontier models what they need. It turned out to also be where the
work got better. Those aren't separate findings.

— Jenny
