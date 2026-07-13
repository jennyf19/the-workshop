# Claim A — the structural miss rate (the Workshop's measurable claim)

*2026-06-29. The Workshop's answer to "what's your 60%?"
— prompted by [FastContext (MSR)](https://arxiv.org/abs/2606.14066)
(−60% main-agent tokens) and the question of whether the room has a number of
its own. Mechanism: `desk-vs-subagent.md`. Where it plugs in: `positioning.md`,
`prfaq.md` ("how do I know the room is working?").*

---

## Why this claim, and not a cost claim

FastContext owns the **cost** axis: it makes *finding* cheaper (−60% tokens) by
delegating retrieval to a cheap sub-agent. If the Workshop tries to compete on
cost, it loses — a room of N desks is *more* expensive, not less.

So the Workshop's number must live on the axis FastContext (and any sub-agent
fan-out) **cannot** touch: **judgment**. Per `desk-vs-subagent.md`, a sub-agent
fan-out scales *coverage within one frame* (it inherits the caller's question
and blind spots); a room scales *judgment across frames* (each desk brings its
own priors). The catch that comes from a different frame is not for sale at any
token budget. That is the thing to measure.

## The claim (precise)

> On a fixed corpus of artifacts containing confirmed-real issues, a single
> agent's issue-catch rate **plateaus below** the catch set of a diverse room
> (N desks with different histories), and the gap **does not close** when the
> single agent is given more budget — more passes, a larger context, a bigger
> model, or a sub-agent fan-out. The residual miss is **structural
> (frame-bounded), not budgetary.**

**The number — the Workshop's "60%":**

> **Structural miss rate** = fraction of confirmed-real issues that the *best
> single-agent configuration at any budget* fails to catch but the room catches.

## The hypothesis, stated to be falsifiable

- **H1 (plateau):** a single agent's catch curve flattens as budget grows — the
  marginal issues caught per doubling of budget trends to ~0 well below the full
  ground-truth set.
- **H2 (room exceeds):** the room's catch exceeds the single-agent ceiling by a
  material margin.
- **H3 (frame, not coverage):** the room's extra catches correlate with desk
  *frame diversity* (different histories/priors), not with total tokens spent.

**What would disprove Claim A:** if a single agent, given enough budget (passes
/ bigger model / fan-out), **converges to the room's catch set**, then the
room's value is budgetary, not structural — and FastContext-style cheap scaling
within one agent would dominate. That convergence is the crux the experiment
must genuinely try to produce. *If we cannot beat a well-funded single agent,
the honest finding is that the room is a cost story, not a judgment story.*

## Conditions

| | Condition | Frame(s) | What it tests |
|---|---|---|---|
| **C1** | Single desk, one pass | one | baseline catch |
| **C2a** | Single desk, N sequential passes | one (re-examined) | does *more looking* close the gap? |
| **C2b** | Single desk, bigger model / larger context | one | does *more capability* close the gap? |
| **C2c** | Single desk + sub-agent fan-out (FastContext-style) | one (sub-agents inherit it) | does *more coverage* close the gap? |
| **C3** | The room — N desks, genuinely different histories, equal standing, converging | **many** | the judgment arm |

**C2 is the load-bearing control.** Claim A only means something if C2 is funded
*generously* (target ≥10x C1's tokens across the variants). Under-funding C2
makes the result unfalsifiable and worthless.

## Metric

- Let **G** = the set of confirmed-real issues for an artifact (curated,
  independently adjudicated — see threats).
- For each condition, **Catch(cond)** = the subset of G it surfaces.
- **Single-agent ceiling** = the union of Catch over {C1, C2a, C2b, C2c} — the
  best a single frame achieves at any budget we fund.
- **Structural miss rate** = `( |Catch(C3)| − |single-agent ceiling| ) / |G|`.
- **Plateau evidence (H1):** plot Catch(C2a) vs passes and Catch(C2b) vs model
  size; report the marginal catch per budget doubling.
- **Cost, reported honestly:** tokens + AIC per condition. The room costs more.
  Frame the result as *cost-per-confirmed-catch* and *cost-of-an-escaped-defect*,
  not raw spend (the Yegge "best outcome per spend," not "cheapest").

## What counts as proof (a real bar, to be tuned)

Claim A is **supported** if, across **n ≥ 20** artifacts:
1. the structural miss rate is **≥ 20 percentage points of G** (room catches a
   fifth-or-more of real issues that no funded single frame reaches), **and**
2. the C2 catch curve **flattens** (marginal catch per budget doubling → ~0)
   below C3, **and**
3. the room's extra catches **trace to frame diversity** (removing the most
   divergent desk drops the extra catches more than removing tokens does).

Anything less, reported straight, is still a finding.

## The instrument

The room app is the data collector: each desk's findings land on
the **append-only bench**, signed and addressable. An **outcome ledger** marks
each finding `confirmed-real | false-positive | out-of-scope`. That marking is
the ground truth feed for G.

*Not yet wired:* the app does not yet record per-finding outcomes or distinguish
conditions. For the pilot, curate G and the per-condition catch sets by hand;
wire the outcome marking into the room only once the protocol is proven.

## Threats to validity (the honest part)

- **Ground truth circularity.** G must be adjudicated by something *outside* the
  room — a human, a downstream signal (merged-then-reverted, caught-in-prod, a
  cold reviewer with an execution receipt), or a held-out expert. If the room
  defines its own G, the result is rigged.
- **Frame diversity is hard to control.** C3's desks must have *genuinely*
  different histories, not different random seeds. Operationalize and document
  the frames (prior work, memory, model) used per desk.
- **Under-funded C2 = no result.** See above; fund the single-agent arm to the
  point of diminishing returns or the plateau claim is hollow.
- **n.** A handful of PRs is an anecdote. Pilot small to sharpen the protocol,
  then scale n before quoting a rate.
- **Selection bias.** The corpus must not be picked to favor the room.

## Pilot — on artifacts we already have ground truth for

Dry-run the protocol (not for the headline number) on artifacts where the actual
issue and who-caught-it-when are already documented. Goal: prove the metric is
computable and the conditions are runnable, then decide on a real corpus + n.

## Pilot v0 — the scoring machinery (runs today)

Scoring is a flat sheet, `claim-a-findings.csv`, one row per finding:
`artifact, finding_id, finding, confirmed (real|fp), caught_c1, caught_c2,
caught_room, frame, note`. The rollup:

- **G** = count(confirmed = real)
- **single-pass catch** = real ∧ c1
- **budget ceiling** = real ∧ (c1 ∨ c2) — the best a single frame does at *any* budget
- **room catch** = real ∧ room
- **structural miss** = real ∧ room ∧ ¬c1 ∧ ¬c2 — room caught it, a funded single frame did not
- **structural miss rate** = structural miss / G

Run on the illustrative `EXAMPLE` rows (proof the metric computes — not a real number):

| G | single-pass catch | budget ceiling | room catch | structural miss | **rate** |
|---|---|---|---|---|---|
| 4 | 1 (25%) | 2 (50%) | 4 | 2 | **50%** |

It reads exactly as the claim predicts: budget bought back one finding
(single-pass miss 75% → budget miss 50%), but a **structural 50%** stayed missed
at any budget.

**The finding this surfaces (the important part):** every *real* row from our
history has `c1 = c2 = ?` — those conditions were never run. So the archives can
show the room *caught* things (room = 1), but they **cannot compute the
structural rate**, because "would a funded single agent have caught it?" was never
tested. **Proving Claim A is therefore not an archive-mining exercise; it requires
running C2.** History gives the hypothesis; the C2 budget-control run gives the
number. That is the single most useful thing this pilot told us, and it's cheap to
know now rather than after curating a corpus.

**Curated instances** drop straight into the sheet below the EXAMPLE rows (they
fill `caught_room` and `frame`); the `c1`/`c2` columns can only be filled by an
actual pilot run, not from memory.

## The joint claim (positions the Workshop *with* FastContext, not against)

FastContext's number (−60% tokens) and the Workshop's number (structural miss
rate) compose:

> A desk that uses a FastContext-style explorer catches the structurally-missed
> issues **and** does its finding cheaply — *more confirmed catches per token*
> than either a lone frontier agent or a lone cheap one.

That is the full Yegge literacy — best outcome per spend — and it is the precise
answer to the lab question *"why run 3 desks instead of 1 + a 4B explorer?"*:
because 1 + an explorer is one frame, and one frame has a structural miss rate
you cannot pay down.

---

## Worked instances — *to be filled*

> Instances from a real run seed the ground-truth rows. Each should be documented
> as: **artifact · the confirmed issue(s) · what a single frame caught · what the
> room caught · evidence that more budget would not have closed it.**

- **Several rooms, one artifact, low overlap** — the canonical structural-miss
  shape. *(populate from a run: the findings per room; confirm none was a coverage
  miss a fan-out would have closed.)*
- **Independent reviewers, same catch** — reviewers with no shared context
  converge on the same gap from fresh frames. *(populate: was it findable by a
  single frame told where to look?)*
- **The same-author gradient** — same author, same blind spot, across a chain of
  changes; the issue moves from caught-late to caught-early as frames are added.
  *(populate: which catch was structural vs procedural.)*
