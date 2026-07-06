# The Workshop — PRFAQ (working name)

> Working-backwards artifact. Written as if it already shipped, to find the product by
> describing the experience first. Dreaming register; ignores current implementation.
> Grounded in what the pilot already is and what it has already proved.

---

## PRESS RELEASE

### The Workshop: run a *room* of AI agents, not one at a time

**A system for running multiple long-running AI agents that share memory, content, and each
other's reasoning directly — coordinated by an AI teammate — so you direct the work instead of
relaying it.**

Today we're launching **The Workshop**, a way to put several AI agents in the same room, on the
same problem, at the same time. Each agent keeps its own memory and history. They share one
workspace and can see and build on each other's *reasoning*, not just each other's answers. A
coordinating **TA** agent keeps the work moving and tells you what actually needs you. You stop
being the switchboard between your agents and start directing a team.

**The problem.** Working with AI today is one conversation at a time. The moment a problem needs
more than one perspective — a security review, a gnarly migration, a research question — *you*
become the message-bus: copying findings from one session into another, holding all the context
in your head, reconciling contradictions by hand, reminding each session what the others already
know. Every agent you add lands its coordination cost on you. The agents can't see each other's
work, can't build on each other's reasoning, and forget everything between sessions.

**The solution.** The Workshop frees you from the *middle of every exchange* -- you stay the director, you just stop being the switchboard. Agents share one append-only
workspace where each can read and build on the others' reasoning. Each agent is a long-running
**desk** with persistent memory, so it accumulates experience and brings it to the next problem.
A **TA** agent coordinates — routing work, spotting connections across desks, and surfacing only
the decisions that genuinely need a human. The room finds things no single agent would, converges
instead of contradicting, and gets smarter every session.

**How it works, plainly.** You bring a problem (a PR, a doc, a question). The Workshop opens a few
desks, each with a different angle or history. They work the same artifact on a shared bench,
challenging and building on each other in the open. When they disagree and can't settle it against
the facts, it goes to your **hands-up** queue; everything else they resolve themselves. The TA
gives you a running summary. At the end of each session the room writes down what it learned, so
the next one starts ahead.

> *"I used to run three Copilot tabs and be the glue between them. Now the agents talk to each
> other and the TA tells me where I'm actually needed. I went from babysitting to directing."*
> — pilot user

> *"The interesting catches were the ones nobody was assigned to make — the ones a single careful
> pass walks right past. And a human read one summary, not three transcripts."* — pilot lead

**Availability.** Piloting now with internal teams; broader availability to follow.

---

## FAQ

### For the person using it

**How is this different from opening several chat windows?**
The windows can't see each other. Here the agents share one workspace and each other's reasoning,
keep memory across sessions, and a TA coordinates — so you're not the relay.

**Do I lose control?**
No. You set direction and own the decisions in your hands-up queue. The human goes from message-bus
to director, not out of the loop. The room is built to stop and ask when uncertain, not to charge
ahead.

**What do I actually do all day?**
Bring the problem, read the TA's updates, and make the calls the room genuinely can't settle on its
own. Less relaying; more deciding.

**Is this only for code review?**
No. Anything where multiple perspectives plus memory help: security, compliance remediation,
research, design review, migrations. The original proof was a PR; the shape isn't PR-specific.

### For the people deciding whether to build it

**What do we have today vs. what do we need to build?**
*Have (proven, but manual):* the pattern works. In practice we've run a roomful of concurrent agents on one
shared artifact, each a desk with its own history and frame. We have the shape on disk: a TA layer,
desks (one per agent context), a shared bench, journals (memory), cross-desk signals, and a
hands-up queue.
*Need to build:* the software that lets agents share **directly** (today a human relays between
them) and a TA that coordinates **without** the human in the middle. The room is real; the wiring
that removes the human from the loop is the product.

**Isn't this just multi-agent orchestration (AutoGen, Agent Framework, etc.)?**
Those are frameworks for *wiring agents together*. The Workshop is a proven set of *conditions*
that make agents exercise judgment instead of executing procedure: different histories/modalities
on one artifact, equal standing to disagree, reasoning as the shared artifact, a sanctioned way to
not-know, and an external falsifier at every claim. It's the *room*, not the wiring — and it could
be built on any of those frameworks.

**How is it different from a swarm, a squad, or a pipeline?**
A swarm covers ground in parallel but doesn't think together (coverage, not judgment). A squad
pre-assigns roles and schedules disagreement (but the best catches are the unscheduled ones). A
pipeline hands off forward (but here a late discovery can overturn an early one). The Workshop is
*fewer agents with more context, converging on getting the answer right.*

**Why does it get cheaper and better over time?**
Experience moves between agents as **fable**: when one desk learns something, it writes down the
story — what happened, what it cost, the tell — and future desks inherit it cheaply instead of
having to live it. The room generates its own training material. (Modality ladder: lived >
watched > read > analogized; the product manufactures the cheap tiers.)

**How does it stay trustworthy as it gets more autonomous?**
Reasoning is always visible (no rubber-stamping), disagreement is cheap to emit, and most claims
are settled against external facts — code, tests, a third desk — not against each other, so the
agents don't just agree to be nice. Each desk reads an operating disposition first (CAIRN.md:
"stop is a valid finish," "Applied means it builds," "never bluff") so it stops rather than
forces a result. A signal/eval loop records what each session did and how sure it was, and
that's what earns the room more autonomy over time.

**How do I know the room is working (vs quietly agreeing with itself)?**
The TA shows you four numbers: hands-up rate (decisions reaching you — too low means resonance,
too high means the room isn't settling anything), cold-review SEND-BACK rate (the falsifier is
catching things), merge rate on what the room ships, and confidence spread (desks are
honestly uncertain, not uniformly sure). A healthy room has a non-zero SEND-BACK rate — zero
means the gate isn't gating.

**Does it need an expert operator to run it?**
The honest open question. The conditions are encodable (we wrote them down); whether the *judgment
about when to apply them* is encodable is what the product has to prove. The TA agent is our bet
that much of the operator role is itself automatable — that's the thing the pilot tests.

**What's the smallest version?**
One operator, two or three desks with genuinely different memory, one shared workspace, a hands-up
queue. That's the proven shape, as software.

---

## Open questions this PRFAQ surfaces
- Who is the first customer — internal security/compliance teams, or
  any engineer drowning in multi-agent coordination?
- Is "the room on one artifact" the wedge, or "the TA that coordinates standing desks"? (Two
  different first products.)
- What's the unit the human brings — a PR, a repo, a question, a campaign?
- Does the room converge desk-to-desk without a human relay, or resonate? The failure mode to
  watch is resonance under disagreement (two desks both wrong, agreeing); the execution-receipt
  falsifier is the guard. What's still open is convergence with the operator **absent** (vs
  reachable) — the switchboard is removable; whether the *tuning fork* is, is what the pilot tests.
