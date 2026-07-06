namespace WorkshopRoom.Data;

// Pure metric computation over the room's in-memory records. This is the
// analytical brain behind the daily pulse and the health read: the live
// session-state path (SessionStoreReader) feeds it and the UI only renders what
// it returns. Kept free of I/O so it is cheap to test (see WorkshopRoom.Tests).
public static class Metrics
{
    public static Health ComputeHealth(
        IReadOnlyList<Desk> desks, IReadOnlyList<BenchTurn> bench,
        IReadOnlyList<HandsUp> handsup, IReadOnlyList<Handoff> handoffs)
    {
        int handsUpOpen = handsup.Count(h => h.Status == "open");
        int reviews = bench.Count(t => t.Kind == "review");
        int sendbacks = bench.Count(t => t.Kind == "review" && t.Body.StartsWith("SEND-BACK", StringComparison.OrdinalIgnoreCase));
        int shipped = handoffs.Count(h => h.State == "done");
        var confs = bench.Where(t => t.Confidence.HasValue).Select(t => t.Confidence!.Value).ToList();
        return new Health(handsUpOpen, sendbacks, reviews, shipped, handoffs.Count,
            confs.Count > 0 ? confs.Min() : null,
            confs.Count > 0 ? confs.Max() : null);
    }

    public static Pulse ComputePulse(
        IReadOnlyList<Desk> desks, IReadOnlyList<BenchTurn> bench,
        IReadOnlyList<HandsUp> handsup, IReadOnlyList<Handoff> handoffs)
    {
        var now = DateTime.UtcNow;
        var checks = new List<PulseCheck>();
        int deskTotal = desks.Count;

        int active = desks.Count(d => (now - d.LastSeen).TotalMinutes <= 60);
        checks.Add(active > 0
            ? new PulseCheck("liveness", "above", $"{active}/{deskTotal} desks active in the last hour")
            : new PulseCheck("liveness", "below", "every desk has gone quiet (>1h)"));

        int moving = bench.Select(t => t.Desk).Distinct().Count();
        checks.Add(deskTotal > 0 && moving >= deskTotal
            ? new PulseCheck("room moving", "above", $"all {deskTotal} desks have moved")
            : new PulseCheck("room moving", "watch", $"{moving}/{deskTotal} desks have moved"));

        int reviews = bench.Count(t => t.Kind == "review");
        int sendbacks = bench.Count(t => t.Kind == "review" && t.Body.StartsWith("SEND-BACK", StringComparison.OrdinalIgnoreCase));
        checks.Add(reviews == 0
            ? new PulseCheck("gate is gating", "unwired", "no cold reviews yet")
            : sendbacks > 0
                ? new PulseCheck("gate is gating", "above", $"{sendbacks}/{reviews} reviews sent back")
                : new PulseCheck("gate is gating", "below", "0 send-backs (gate not gating)"));

        var confs = bench.Where(t => t.Confidence.HasValue).Select(t => t.Confidence!.Value).ToList();
        checks.Add(confs.Count == 0
            ? new PulseCheck("honest uncertainty", "unwired", "no confidence signal yet")
            : (confs.Max() - confs.Min()) > 0.0001
                ? new PulseCheck("honest uncertainty", "above", $"confidence varies {confs.Min():0.00}-{confs.Max():0.00}")
                : new PulseCheck("honest uncertainty", "below", "uniform confidence (resonance risk)"));

        int shipped = handoffs.Count(h => h.State == "done");
        checks.Add(handoffs.Count == 0
            ? new PulseCheck("work shipping", "unwired", "no handoffs tracked yet")
            : shipped > 0
                ? new PulseCheck("work shipping", "above", $"{shipped}/{handoffs.Count} handoffs shipped")
                : new PulseCheck("work shipping", "watch", "nothing shipped yet"));

        int hu = handsup.Count(h => h.Status == "open");
        checks.Add(hu == 0
            ? new PulseCheck("operator queue", "watch", "nothing queued (self-sufficient, or resonance)")
            : hu <= 3
                ? new PulseCheck("operator queue", "above", $"{hu} decision(s) queued (healthy)")
                : new PulseCheck("operator queue", "below", $"{hu} queued (overflow)"));

        int below = checks.Count(x => x.Verdict == "below");
        int above = checks.Count(x => x.Verdict == "above");
        string temp = below > 0 ? "below the line" : above >= 2 ? "above the line" : "warming up";
        string headline = below > 0
            ? "a signal dropped below the line; check the red marker"
            : above >= 2
                ? "the room is healthy on the signals it can see"
                : "too few signals wired to call it yet";
        return new Pulse(temp, headline, checks);
    }
}
