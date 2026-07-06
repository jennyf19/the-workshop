namespace WorkshopRoom.Tests;

// Builders for the room's records, so each test states only the fields it
// cares about and reads as intent rather than positional noise. Defaults are
// deliberately benign (one active desk, an unsigned note, an open hands-up /
// handoff); tests override just the field under test.
internal static class Sample
{
    public static Desk Desk(string name = "desk", DateTime? lastSeen = null, string status = "active")
        => new(Id: 1, Name: name, Modality: "lived", Model: "a-model", Status: status,
               Note: "", LastSeen: lastSeen ?? DateTime.UtcNow, TokensIn: 0, TokensOut: 0,
               Aic: 0, SessionId: "sess", Cwd: @"D:\x", ConsolePid: 0, Sync: "synced");

    public static BenchTurn Turn(string desk = "desk", string kind = "note",
                                 string body = "", double? confidence = null)
        => new(Id: 1, Seq: 1, Desk: desk, Kind: kind, Body: body,
               Confidence: confidence, SignedBy: desk, Ts: DateTime.UtcNow);

    public static HandsUp HandsUp(string status = "open")
        => new(Id: 1, Question: "why?", RaisedBy: "desk", Status: status, Ts: DateTime.UtcNow);

    public static Handoff Handoff(string state = "open")
        => new(Id: 1, Task: "do the thing", SourceDesk: "desk", Scope: "scope",
               Sources: "src", State: state, ClaimedBy: null, Receipt: null, Ts: DateTime.UtcNow);
}
