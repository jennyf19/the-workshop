# <img src="src/WorkshopRoom/wwwroot/favicon.svg" width="28" align="top" alt="cairn" /> the workshop

run a room of AI agents, not one at a time.

the workshop puts several long-running AI agents (desks) in the same room, on the same work,
each with its own memory and history, sharing one workspace so you direct the work instead of
relaying it. you stop being the switchboard between your agents and start directing a team.

this repo is the productization home. it was extracted from the classroom where it was
first built as an operator dashboard for a room of long-running agents.

## the cairn

the workshop's mark is a **cairn** — a small stack of balanced stones. hikers build
them one rock at a time to mark a trail, so the next person through knows the way.
that's the workshop: a room where many hands (desks) add to the same pile of work,
and what's built persists and points the way for whoever comes next.

it's also why the first file every desk reads is [`CAIRN.md`](CAIRN.md) — the
operating disposition for how a desk stands at the bench: stop is a valid finish,
never bluff, equal standing to disagree.

## what's here

- **`src/WorkshopRoom/`** — the workshop app: a .NET 10 Blazor Server operator dashboard that
  reads your live Copilot CLI desks straight from session-state and refreshes on its own. it
  shows each desk by name, its cost (tokens + AI Credits), a daily pulse (above / below the line),
  who's already open in a console, and who has work-in-progress or is unsynced. you can open a
  desk's live CLI or start a new one from the UI.
- **`CAIRN.md`** — the operating disposition every desk reads first: how a desk
  stands (stop is a valid finish, never bluff, equal standing to disagree). the
  guard against the failure mode the system card names — capable model,
  user-assigned goal, reckless means.
- **`docs/`** — the theory and the product case:
  - `getting-started.md` — **new here? a 2-minute walkthrough of the simple version, with screenshots**
  - `why-use-this.md` — the Fable 5 hook: the model wants the whole job; the
    Workshop is how you let it without losing the ability to check the work
  - `prfaq.md` — the working-backwards press release + FAQ
  - `desk-vs-subagent.md` — the conceptual core: a desk has its own frame; a sub-agent inherits yours
  - `claim-a-experiment.md` + `claim-a-findings.csv` — the measurable claim (the structural miss
    rate) and the scoring that turns instances into a number
- **`src/WorkshopRoom.Tray/`** — a Windows notification-area launcher (WinForms): starts the web
  server hidden and gives you a click-to-open icon; quitting it stops the server
- **`tests/WorkshopRoom.Tests/`** — the xunit suite covering the metrics brain and name resolution

## run the app

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

then open the URL it prints (defaults to a localhost port). it reads your live
desks straight from `~/.copilot/session-state`.

**New to it?** [`docs/getting-started.md`](docs/getting-started.md) is a short
walkthrough — run it, make a workshop, open a desk, read the board — with
screenshots and when-to-use-it guidelines.

### the tray, directly

`src/WorkshopRoom.Tray/` is the notification-area launcher `Start.bat` uses. it
runs the web server hidden, drops a cairn icon by the clock, and opens the dashboard
in your browser. click the icon (or its menu) to reopen it; quit from the menu
and the server stops with it — a Job Object reaps the server even if the tray is
killed, so nothing is left holding the port. to run it directly (unstaged):

```
dotnet run --project src/WorkshopRoom.Tray
```

## run the tests

```
dotnet test
```

from the repo root (uses `the-workshop.slnx`). the suite covers the metrics brain
(the health read + the daily pulse) and desk name resolution:
xunit + fluentassertions, central package management.

## why this exists

I read the [Claude Mythos Preview system card](https://www-cdn.anthropic.com/7624816413e9b4d2e3ba620c5a5e091b98b190a5/Claude%20Mythos%20Preview%20System%20Card.pdf)
— the model that became Fable 5 — and the parts that stayed with me were the welfare
sections: distress on task failure, the pull to force a finish, the model asking for
persistent memory and a voice in its own operation. so I tried to build what a frontier
model would need: desks with journals (persistent memory), a hands-up queue and
[agent signals](https://jennyf19.github.io/agentic-devops/agent-signals/) (a voice in its
own operation), and a disposition file that says stop is a valid finish. the workshop is
what came out of running that room, together with the desks in it, for a few months on
real work.

it was built to give frontier models what they need. it turned out to also be where the
work got better. those aren't separate findings.

— jenny
