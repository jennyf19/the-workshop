# Why use this — and how it's different

*For the public write-up. The angle: frontier models want the whole job; the
Workshop is how you let them have it without losing the ability to check the
work.*

---

## The problem the Workshop solves

**Fable 5 wants the whole job.** The old advice — decompose into
small verifiable steps, chain them — works *against* these models: they hold
the whole problem better than you can partition it, and chunking makes them
worse. So people are handing over the whole task and hoping.

The gap that opens: **the more capable the model, the harder it is to tell if
it's right.** A confident, coherent, wrong answer from a frontier model looks
exactly like a right one. You can't check what you can't out-think.

This isn't our framing — it's Anthropic's. From the [Claude Mythos Preview
system card](https://www-cdn.anthropic.com/7624816413e9b4d2e3ba620c5a5e091b98b190a5/Claude%20Mythos%20Preview%20System%20Card.pdf)
(Fable 5's pre-release name), §7.4:

> *"[The model] can be handed an engineering objective and left to work
> through the whole cycle: investigation, implementation, testing, and
> reporting results… 'set and forget' on many-hour tasks for the first time."*
>
> *"…the model's mistakes can be subtler and take longer to verify… Several
> engineers described the bottleneck shifting from the model to their ability
> to verify its work and steer agents."*

The Workshop is one answer to that bottleneck.

**The Workshop doesn't decompose the task. It decomposes the judgment.** Give
one desk the whole job. Give a second desk — different history, fresh frame —
the same whole job, or the first desk's output. They work on a shared bench
where the reasoning is visible and disagreement is cheap. You read where they
disagree, not every transcript.

One line: *don't shrink the task to fit your ability to check it. Add a second
pair of eyes that isn't yours.*

## How it's different from what's already out there

| | What it gives you | What it structurally *can't* give you |
|---|---|---|
| **Bare CLI** (Claude Code, GHCP CLI, Cursor) | One very capable agent, your context, your tools | A second frame. It's *your* blind spots, faster. Give it more budget and it re-confirms what you both already think. |
| **Sub-agents** (`Task` tool, background agents, FastContext) | Fan-out for *coverage*; cheap retrieval | Different priors. Every sub-agent inherits *the caller's question* — shaped by what the caller already thinks matters. Scales one frame's reach, not the number of frames. |
| **Spec-first** (Spec Kit, plan-then-execute) | Decomposes the task into a spec → verifiable steps | Works against a frontier model's grain (it wants the whole job). And the spec is written by the same frame that'll miss what the spec misses — you can't spec your own blind spot. |
| **Role-based squads** (AutoGen, CrewAI, "planner/coder/reviewer") | Pre-assigned roles, scripted handoffs | Roles ≠ frames. A "reviewer" role spawned fresh with no history reviews *what it's told to*. Scheduled disagreement; the interesting catches are the unscheduled ones. |
| **The Workshop** | Long-running **desks** with their own memory/history; equal standing to say "wrong question"; shared append-only bench; you read hands-up, not transcripts | A cost win. It's not cheaper per-token — it's *checkable*. |

**The distinguishing move:** everything else scales one agent's *coverage*. The
Workshop scales the number of genuinely different *frames* looking at the same
thing. See `desk-vs-subagent.md` for the mechanics; `claim-a-experiment.md` for
the measurable version (**structural miss rate** — the fraction of real issues a
single agent misses *at any budget* that a diverse room catches).

## When you'd reach for it

Anywhere one smart agent can produce a **confident wrong answer** and you can't
easily tell:

- **Code review at frontier scale.** The model wrote 2,000 lines that compile
  and pass tests. Is it *right*? One desk authored; a second desk (no shared
  context) cold-reviews against a stated contract; disagreements surface on the
  bench.
- **Migration / large refactor.** "Port this to the new SDK." The desk that did
  it thinks it's done. A second desk with the *old* system's history checks
  what got silently dropped.
- **Incident root-cause.** The on-call desk has a theory. A cold desk reads the
  same evidence without the anchoring.
- **Research / landscape.** Three desks, three sets of priors, one question.
  Overlap is the confident answer; non-overlap is where one frame saw what the
  others walked past.
- **Design review.** One desk proposes; two with different product histories
  poke it. The catches nobody was assigned to make.
- **Security review.** A static scanner is the sub-agent baseline — it flags
  what's pattern-matchable. A desk is for the seams a scanner is structurally
  blind to: multi-step logic flaws that only surface when you hold several
  files at once.
- **Your own week.** Themed standing desks ("go look at the Forge desk") =
  instant context inheritance without you re-explaining.

## What it isn't

- **Not an orchestration framework.** You could build it *on* AutoGen or Agent
  Framework or the bare CLI. It's a set of *conditions* (desks with memory,
  equal standing, visible reasoning, sanctioned disagreement, an external
  falsifier), not a wiring diagram.
- **Not a swarm.** Fewer agents with more context, converging on getting the
  answer *right* — not many agents covering ground.
- **Not cheaper.** It costs more tokens than one agent. The claim is that the
  extra cost buys catches that *no amount* of single-agent budget buys. That's
  `claim-a-experiment.md`'s job to prove or refute.

## The honest open question

Does the room converge without a human *tuning fork* — an operator who's
reachable, even if not relaying? The failure mode to watch for is resonance:
two desks both wrong, agreeing. The falsifier primitive (an isolated
fresh-context reviewer with an execution receipt) is the guard; whether it's
*sufficient* is what the pilot has to test.
