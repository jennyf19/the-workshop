# Desks vs sub-agents

*The conceptual core: what makes a desk different from a sub-agent, and why
that difference is where the Workshop's value lives. The industry is converging
on sub-agents — this is the answer to "do you still need the room?"*

---

## The short answer

Yes, the Workshop still has value — and the rise of sub-agents sharpens *why*
rather than threatening it. **A sub-agent is a tool with a brain. A desk is a
peer with a history.** However good a sub-agent gets, it's still a tool. The
Workshop is about desks.

## The distinction

| | **Sub-agent / task** | **Desk / fresh agent** |
|---|---|---|
| **Lifecycle** | One-shot. Spawned, runs, returns, dies. | Long-running. Sits at a desk across sessions. |
| **State** | Stateless. Each spawn is blank. | Has memory (journal, auto-memory). Accumulates. |
| **Frame** | Inherits the caller's frame — answers *the caller's question*. | Has its own frame — different history, different priors, different things it's primed to notice. |
| **Relationship** | Hierarchical. Caller owns judgment; sub-agent executes. | Peer. Equal standing to disagree, to say "you're asking the wrong question." |
| **What it scales** | **Coverage.** One agent fans out to N sub-agents to cover ground. | **Judgment.** N desks with different histories on one artifact catch what one wouldn't. |
| **Examples** | the harness `Explore` agent, a cold-review gate, the `Agent` tool, AutoGen workers | The standing desks in an operator's own day; long-running peers on one artifact |
| **Failure mode** | Returns a wrong answer to the right question. | Resonance — agrees with peers when both are wrong. |

**Mechanically:** a desk *is built out of* sub-agent invocations. Sub-agents are stateless and one-shot in every
harness we have; "back-and-forth" = the orchestrator re-spawns a fresh one each
round. So a desk = repeated fresh spawns + persistent state on disk
(journal/memory) that gets read at each spawn + a stable identity/role. The
mechanism is the same; the *semantics* — what the spawn is *for* — are
different.

## Why the difference matters: frame, not coverage

Put several desks — genuinely different histories — on one artifact, and the
value isn't that they read more of it. It's that each brings different patterns
it has seen, different things it's primed to notice. Where they *don't* overlap
is where one frame caught what the others walked past. A sub-agent fan-out
covers more *files*; it doesn't bring more *priors* — every sub-agent inherits
the caller's question, which is shaped by the caller's blind spots. The catch
that comes from a genuinely different frame is the thing a fan-out can't buy at
any budget.

## Give every desk a fast explorer

A purpose-trained retrieval sub-agent — one that fans out Read/Glob/Grep in
parallel and returns tight `file:line` citations — makes any *one* agent's
retrieval cheaper. It does not:
- bring a different prior (it answers *your* question)
- disagree with you (it's not asked to)
- accumulate experience (stateless)
- catch the question you didn't ask

So the relationship is simple: **give every desk a fast explorer.** Each desk
gets cheaper to run; the room doesn't get smaller. It's a cost lever, not a seat
at the table — which pre-empts the obvious question: *"why run 3 desks when I
could run 1 agent + a cheap explorer?"* The explorer *finds*; it doesn't
disagree, verify, or notice what no one asked about. The room is about
*judgment*, not *retrieval*.

## The hybrid: the cold-review gate

Worth naming because it muddies the line usefully. A **cold-review gate** is
*mechanically* a sub-agent (one-shot, spawned by an orchestrator, returns a
verdict). But it's *deliberately fresh-context* — the wall: it doesn't inherit
the author's reasoning. It's a sub-agent **designed to behave like a desk**
for one turn: bring a different frame, be free to say SEND-BACK.

That's the bridge. You can build desk-like behavior out of sub-agent
mechanics — *if* you give the spawn its own frame (don't pass the author's
context), equal standing (its verdict is binding), and a way to disagree
(SEND-BACK is a valid output). A plain retrieval sub-agent has none of those by
design; a cold-review gate has all three. Same spawning mechanism, opposite
semantics.

## So: does the Workshop still have value?

**Yes — and the sub-agent comparison is what makes the value precise.**

The industry is converging on sub-agents (AutoGen, Agent Framework, the `Agent`
tool in every harness, purpose-trained retrieval models). Those scale *coverage*
and cut *cost*. They're necessary and the Workshop should use them.

What none of them give you is a **room**: multiple long-running peers with
different histories and equal standing, converging on one artifact, free to
disagree, with a human reading hands-up instead of every transcript. That's
the bet. A sub-agent makes each seat at the table cheaper to run; it doesn't
add a seat.

The honest open question (`prfaq.md`) is unchanged: does the room
converge without the human tuning fork? That's about desk-to-desk dynamics.
Sub-agents are orthogonal to it.

---

**See also:** `prfaq.md` "isn't this just multi-agent
orchestration?" (this doc is the longer answer to that FAQ);
`claim-a-experiment.md` (the measurable version — structural miss rate).
