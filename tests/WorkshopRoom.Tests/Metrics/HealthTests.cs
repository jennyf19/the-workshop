namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="Metrics.ComputeHealth"/> — the few numbers that tell the
/// operator the room is actually working (and not just quietly agreeing with
/// itself). Send-backs count only on <c>review</c> turns whose body starts with
/// "SEND-BACK" (case-insensitive); the confidence band ignores turns that carry
/// no confidence signal.
/// </summary>
public class HealthTests
{
    static readonly IReadOnlyList<Desk> NoDesks = Array.Empty<Desk>();
    static readonly IReadOnlyList<BenchTurn> NoBench = Array.Empty<BenchTurn>();
    static readonly IReadOnlyList<HandsUp> NoHands = Array.Empty<HandsUp>();
    static readonly IReadOnlyList<Handoff> NoHandoffs = Array.Empty<Handoff>();

    [Fact]
    public void Empty_room_is_all_zeros_with_no_confidence()
    {
        var h = Metrics.ComputeHealth(NoDesks, NoBench, NoHands, NoHandoffs);

        h.HandsUpOpen.Should().Be(0);
        h.Reviews.Should().Be(0);
        h.SendBacks.Should().Be(0);
        h.Shipped.Should().Be(0);
        h.Handoffs.Should().Be(0);
        h.ConfMin.Should().BeNull();
        h.ConfMax.Should().BeNull();
    }

    [Fact]
    public void Counts_only_open_hands_up()
    {
        var hands = new[] { Sample.HandsUp("open"), Sample.HandsUp("open"), Sample.HandsUp("answered") };

        Metrics.ComputeHealth(NoDesks, NoBench, hands, NoHandoffs)
            .HandsUpOpen.Should().Be(2);
    }

    [Fact]
    public void Send_backs_count_only_review_turns_with_the_sendback_prefix()
    {
        var bench = new[]
        {
            Sample.Turn(kind: "review", body: "SEND-BACK: missing test"),
            Sample.Turn(kind: "review", body: "send-back: lowercase still counts"),
            Sample.Turn(kind: "review", body: "PASS: looks good"),
            Sample.Turn(kind: "note",   body: "SEND-BACK: not a review, ignored"),
        };

        var h = Metrics.ComputeHealth(NoDesks, bench, NoHands, NoHandoffs);

        h.Reviews.Should().Be(3);    // three review turns; the note is not a review
        h.SendBacks.Should().Be(2);  // two of those reviews are send-backs (case-insensitive)
    }

    [Fact]
    public void Shipped_counts_only_done_handoffs()
    {
        var handoffs = new[] { Sample.Handoff("done"), Sample.Handoff("open"), Sample.Handoff("done") };

        var h = Metrics.ComputeHealth(NoDesks, NoBench, NoHands, handoffs);

        h.Shipped.Should().Be(2);
        h.Handoffs.Should().Be(3);
    }

    [Fact]
    public void Confidence_band_spans_min_and_max_ignoring_unset_turns()
    {
        var bench = new[]
        {
            Sample.Turn(confidence: 0.4),
            Sample.Turn(confidence: 0.9),
            Sample.Turn(confidence: null),  // no signal — ignored
        };

        var h = Metrics.ComputeHealth(NoDesks, bench, NoHands, NoHandoffs);

        h.ConfMin.Should().Be(0.4);
        h.ConfMax.Should().Be(0.9);
    }
}
