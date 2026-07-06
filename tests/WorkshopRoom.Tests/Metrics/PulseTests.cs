namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="Metrics.ComputePulse"/> — the daily pulse the operator
/// glances at each morning. Each of the six signals lands "above" / "below" /
/// "watch" / "unwired", and the temperature rolls them up: any "below" pulls the
/// room below the line; otherwise two or more "above" reads above the line; else
/// it is still "warming up".
/// </summary>
public class PulseTests
{
    static readonly IReadOnlyList<BenchTurn> NoBench = Array.Empty<BenchTurn>();
    static readonly IReadOnlyList<HandsUp> NoHands = Array.Empty<HandsUp>();
    static readonly IReadOnlyList<Handoff> NoHandoffs = Array.Empty<Handoff>();

    static PulseCheck Check(Pulse p, string name) => p.Checks.Single(c => c.Name == name);

    static Pulse Run(IReadOnlyList<Desk>? desks = null, IReadOnlyList<BenchTurn>? bench = null,
                     IReadOnlyList<HandsUp>? hands = null, IReadOnlyList<Handoff>? handoffs = null)
        => Metrics.ComputePulse(desks ?? Array.Empty<Desk>(), bench ?? NoBench,
                                hands ?? NoHands, handoffs ?? NoHandoffs);

    // --- liveness: a desk seen in the last hour ---

    [Fact]
    public void Liveness_is_above_when_a_desk_was_seen_within_the_hour()
        => Check(Run(desks: new[] { Sample.Desk(lastSeen: DateTime.UtcNow) }), "liveness")
            .Verdict.Should().Be("above");

    [Fact]
    public void Liveness_is_below_when_every_desk_has_gone_quiet()
        => Check(Run(desks: new[] { Sample.Desk(lastSeen: DateTime.UtcNow.AddHours(-2)) }), "liveness")
            .Verdict.Should().Be("below");

    // --- room moving: every desk has a bench turn ---

    [Fact]
    public void Room_moving_is_above_when_every_desk_has_a_bench_turn()
    {
        var desks = new[] { Sample.Desk(name: "a"), Sample.Desk(name: "b") };
        var bench = new[] { Sample.Turn(desk: "a"), Sample.Turn(desk: "b") };

        Check(Run(desks, bench), "room moving").Verdict.Should().Be("above");
    }

    [Fact]
    public void Room_moving_is_watch_when_a_desk_is_still_silent()
    {
        var desks = new[] { Sample.Desk(name: "a"), Sample.Desk(name: "b") };
        var bench = new[] { Sample.Turn(desk: "a") };

        Check(Run(desks, bench), "room moving").Verdict.Should().Be("watch");
    }

    // --- gate is gating: send-backs on reviews ---

    [Fact]
    public void Gate_is_unwired_with_no_reviews()
        => Check(Run(), "gate is gating").Verdict.Should().Be("unwired");

    [Fact]
    public void Gate_is_below_when_reviews_have_no_send_backs()
    {
        var bench = new[] { Sample.Turn(kind: "review", body: "PASS") };

        Check(Run(bench: bench), "gate is gating").Verdict.Should().Be("below");
    }

    [Fact]
    public void Gate_is_above_when_a_review_is_sent_back()
    {
        var bench = new[]
        {
            Sample.Turn(kind: "review", body: "PASS"),
            Sample.Turn(kind: "review", body: "send-back: needs work"),
        };

        Check(Run(bench: bench), "gate is gating").Verdict.Should().Be("above");
    }

    // --- honest uncertainty: confidence must vary ---

    [Fact]
    public void Honest_uncertainty_is_unwired_without_any_confidence_signal()
        => Check(Run(bench: new[] { Sample.Turn() }), "honest uncertainty").Verdict.Should().Be("unwired");

    [Fact]
    public void Honest_uncertainty_is_below_when_confidence_is_uniform()
    {
        var bench = new[] { Sample.Turn(confidence: 0.8), Sample.Turn(confidence: 0.8) };

        Check(Run(bench: bench), "honest uncertainty").Verdict.Should().Be("below");
    }

    [Fact]
    public void Honest_uncertainty_is_above_when_confidence_varies()
    {
        var bench = new[] { Sample.Turn(confidence: 0.3), Sample.Turn(confidence: 0.95) };

        Check(Run(bench: bench), "honest uncertainty").Verdict.Should().Be("above");
    }

    // --- work shipping: handoffs reaching done ---

    [Fact]
    public void Work_shipping_is_unwired_with_no_handoffs()
        => Check(Run(), "work shipping").Verdict.Should().Be("unwired");

    [Fact]
    public void Work_shipping_is_watch_when_nothing_is_done()
        => Check(Run(handoffs: new[] { Sample.Handoff("open") }), "work shipping").Verdict.Should().Be("watch");

    [Fact]
    public void Work_shipping_is_above_when_something_shipped()
        => Check(Run(handoffs: new[] { Sample.Handoff("done") }), "work shipping").Verdict.Should().Be("above");

    // --- operator queue: open hands-up, healthy in small numbers ---

    [Fact]
    public void Operator_queue_is_watch_when_empty()
        => Check(Run(), "operator queue").Verdict.Should().Be("watch");

    [Fact]
    public void Operator_queue_is_above_with_a_handful_of_decisions()
    {
        var hands = new[] { Sample.HandsUp(), Sample.HandsUp(), Sample.HandsUp() };

        Check(Run(hands: hands), "operator queue").Verdict.Should().Be("above");
    }

    [Fact]
    public void Operator_queue_is_below_when_it_overflows()
    {
        var hands = Enumerable.Range(0, 4).Select(_ => Sample.HandsUp()).ToArray();

        Check(Run(hands: hands), "operator queue").Verdict.Should().Be("below");
    }

    // --- temperature rollup ---

    [Fact]
    public void Temperature_is_below_the_line_when_any_signal_is_below()
    {
        // active desk (above) + a review with no send-back (below) → below wins
        var desks = new[] { Sample.Desk(lastSeen: DateTime.UtcNow) };
        var bench = new[] { Sample.Turn(desk: "desk", kind: "review", body: "PASS") };

        Run(desks, bench).Temperature.Should().Be("below the line");
    }

    [Fact]
    public void Temperature_is_above_the_line_with_two_aboves_and_nothing_below()
    {
        // liveness above + room moving above; everything else unwired / watch
        var desks = new[] { Sample.Desk(name: "desk", lastSeen: DateTime.UtcNow) };
        var bench = new[] { Sample.Turn(desk: "desk", kind: "note") };

        Run(desks, bench).Temperature.Should().Be("above the line");
    }

    [Fact]
    public void Temperature_is_warming_up_when_only_one_signal_is_above()
    {
        // one active desk (liveness above) but nothing else wired
        var desks = new[] { Sample.Desk(lastSeen: DateTime.UtcNow) };

        Run(desks).Temperature.Should().Be("warming up");
    }
}
