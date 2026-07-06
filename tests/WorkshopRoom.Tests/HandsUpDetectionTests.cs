namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="SessionStoreReader.OperatorQuestion"/> — the automatic
/// "raise a hand" sweep. A desk is waiting on the operator when it is blocked on
/// an unanswered ask_user (the hard signal, however long ago it asked), or when
/// it has gone idle and its last assistant message ended on a question (the soft
/// signal). Otherwise the room is settling its own work and nothing is queued.
/// </summary>
public class HandsUpDetectionTests
{
    [Fact]
    public void Unanswered_ask_user_is_a_hand_raised_even_while_active()
        => SessionStoreReader.OperatorQuestion("which direction do you want?", lastAssistant: null, idleMinutes: 0)
            .Should().Be("which direction do you want?");

    [Fact]
    public void Pending_question_is_trimmed()
        => SessionStoreReader.OperatorQuestion("  pick one  ", null, 0).Should().Be("pick one");

    [Fact]
    public void Idle_desk_that_ended_on_a_question_raises_a_hand()
        => SessionStoreReader.OperatorQuestion(null, "should I ship this or wait?", idleMinutes: 45)
            .Should().Be("should I ship this or wait?");

    [Fact]
    public void Idle_desk_that_did_not_end_on_a_question_stays_quiet()
        => SessionStoreReader.OperatorQuestion(null, "shipped it, all green.", idleMinutes: 45)
            .Should().BeNull();

    [Fact]
    public void Active_desk_without_a_pending_ask_is_not_flagged()
        => SessionStoreReader.OperatorQuestion(null, "still working on it?", idleMinutes: 2)
            .Should().BeNull();

    [Fact]
    public void No_signal_at_all_is_null()
        => SessionStoreReader.OperatorQuestion(null, null, 999).Should().BeNull();

    [Fact]
    public void Blank_pending_falls_through_to_the_soft_signal()
        => SessionStoreReader.OperatorQuestion("   ", "ready to merge?", 30)
            .Should().Be("ready to merge?");
}
