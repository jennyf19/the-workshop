# the workshop

run a room of AI agents, not one at a time.

the workshop puts several long-running AI agents (desks) in the same room, on the same work,
each with its own memory and history, sharing one workspace so you direct the work instead of
relaying it. you stop being the switchboard between your agents and start directing a team.

this repo is the productization home. it was extracted from the classroom where it was
first built as an operator dashboard for a room of long-running agents.

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
  - `why-use-this.md` — the Fable 5 hook: the model wants the whole job; the
    Workshop is how you let it without losing the ability to check the work
  - `prfaq.md` — the working-backwards press release + FAQ
  - `positioning.md` — why this is what Satya and Yegge are both pointing at
  - `desk-vs-subagent.md` — the conceptual core: a desk has its own frame; a sub-agent inherits yours
  - `claim-a-experiment.md` + `claim-a-findings.csv` — the measurable claim (the structural miss
    rate) and the scoring that turns instances into a number
- **`src/WorkshopRoom.Tray/`** — a Windows notification-area launcher (WinForms): starts the web
  server hidden and gives you a click-to-open icon; quitting it stops the server
- **`tests/WorkshopRoom.Tests/`** — the xunit suite covering the metrics brain and name resolution

## run the app

```
cd src/WorkshopRoom
dotnet run
```

then open the URL it prints (defaults to a localhost port). it reads your live
desks straight from `~/.copilot/session-state`.

### or run it from the tray (Windows)

`src/WorkshopRoom.Tray/` is a notification-area launcher — one thing you start.
it runs the web server hidden, drops a **w** icon by the clock, and opens the
dashboard in your browser. click the icon (or its menu) to reopen it; quit from
the menu and the server stops with it — and a Job Object reaps the server even
if the tray is killed, so nothing is left holding the port.

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

## status

incubating, private. the app builds and runs; the measurable claim is designed down to the one
experiment that proves it (a budget-controlled single-agent run, not archive-mining). when the
narrative is ready, the public write-up lands in `jennyf19/agentic-devops`; the app ships from
here.
