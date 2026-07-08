namespace WorkshopRoom.Data;

// The room's truth, as plain records. These shapes are the durable asset:
// the UI is a skin over them, and they survive a UI rewrite ("the data is
// the truth, the UI is just a view").

public record Artifact(int Id, string Ref, string Title);

// A long-running agent context. Modality = the workshop's experience ladder
// (lived > watched > read). Status = active / idle / hands-up.
public record Desk(
    int Id, string Name, string Modality, string Model,
    string Status, string Note, DateTime LastSeen,
    long TokensIn, long TokensOut, long Aic,
    string SessionId, string Cwd, int ConsolePid, string Sync);

// One append-only, signed turn on the shared bench. Seq is its stable
// identity within an artifact (DeltaDB's "every operation gets an identity").
// Kind: claim / contract / finding / review / receipt / note / handoff.
public record BenchTurn(
    int Id, int Seq, string Desk, string Kind, string Body,
    double? Confidence, string SignedBy, DateTime Ts);

// A decision the room could not settle against the facts, surfaced to the
// operator. This is the only thing the operator is asked to read.
public record HandsUp(int Id, string Question, string RaisedBy, string Status, DateTime Ts);

// The handoff record: one desk's result becomes the next desk's task, with
// sources attached, scope visible, and a receipt. State carries Open Engine's
// verbs: open / claimed / paused / done.
public record Handoff(
    int Id, string Task, string SourceDesk, string Scope, string Sources,
    string State, string? ClaimedBy, string? Receipt, DateTime Ts);

// The four numbers that tell the operator the room is working (and not just
// quietly agreeing with itself). A non-zero send-back rate is healthy.
public record Health(
    int HandsUpOpen, int SendBacks, int Reviews,
    int Shipped, int Handoffs, double? ConfMin, double? ConfMax);

// The daily pulse: one signal evaluated above / below the line (or watch /
// unwired when the signal isn't strong enough to judge). Temperature rolls
// them up into a single read the operator can glance at each morning.
public record PulseCheck(string Name, string Verdict, string Detail);

public record Pulse(string Temperature, string Headline, List<PulseCheck> Checks);

// An agent signal emitted by a desk — the structured self-report that gives
// the operator live insight into what the agent thinks about its own work.
// Signal types: execution (after a task), escalation (needs help), partnership
// (one agent reviewing another), outcome (independent eval).
public record AgentSignal(
    string SignalType,           // execution | escalation | partnership | outcome
    string DeskName,            // which desk emitted this
    string AgentName,           // agent_name from the signal
    int Confidence,             // self_assessment.confidence (1-5)
    int Accuracy,               // self_assessment.accuracy (1-5)
    int Completeness,           // self_assessment.completeness (1-5)
    string WhatWorked,          // patterns.what_worked
    string WhatWasHard,         // patterns.what_was_hard
    string SkillGap,            // patterns.skill_gap
    string? EscalationReason,   // escalation.reason (null if not an escalation)
    string? EscalationBlocked,  // escalation.blocked_on
    string? Recommendation,     // escalation.recommendation
    DateTime EmittedAt,         // file mtime (when the signal was written)
    string FilePath);           // full path to the signal file
