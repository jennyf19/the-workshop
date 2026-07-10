namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="DeskAgentStore"/> — how the room remembers which CLI
/// launched a desk. Agency drives Copilot under the hood, so on disk the two are
/// indistinguishable; the room records a launch by folder and binds it to the
/// first session that shows up there (issue #2). A desk the room didn't launch
/// (e.g. a Copilot session started outside the UI) has no record and correctly
/// defaults to Copilot.
/// </summary>
public class DeskAgentStoreTests
{
    [Fact]
    public void Resolve_defaults_to_copilot_when_nothing_was_recorded()
    {
        var r = new DeskAgentStore(null).Resolve("sess-1", @"D:\ws", createdUtc: null);
        r.agent.Should().Be("copilot");
        r.name.Should().BeNull();
    }

    [Fact]
    public void A_recorded_launch_binds_to_the_next_session_in_that_folder()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws", "agency", "reviewer");

        var r = store.Resolve("sess-1", @"D:\ws", DateTime.UtcNow);
        r.agent.Should().Be("agency");
        r.name.Should().Be("reviewer");
    }

    [Fact]
    public void A_binding_is_stable_by_session_id_and_consumes_the_pending_launch()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws", "agency", "reviewer");
        store.Resolve("sess-1", @"D:\ws", DateTime.UtcNow);   // binds + consumes

        // A second desk in the same folder with no new launch is plain Copilot...
        store.Resolve("sess-2", @"D:\ws", DateTime.UtcNow).agent.Should().Be("copilot");
        // ...and the first session stays bound to Agency.
        store.Resolve("sess-1", @"D:\ws", DateTime.UtcNow).agent.Should().Be("agency");
    }

    [Fact]
    public void Copilot_launches_are_not_recorded()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws", "copilot", "plain");
        store.Resolve("sess-1", @"D:\ws", DateTime.UtcNow).agent.Should().Be("copilot");
    }

    [Fact]
    public void A_session_that_predates_the_launch_does_not_bind()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws", "agency", "reviewer");
        // A session born a minute before we launched can't be the one we started.
        store.Resolve("old", @"D:\ws", DateTime.UtcNow.AddMinutes(-1)).agent.Should().Be("copilot");
    }

    [Fact]
    public void A_launch_in_a_different_folder_does_not_bind()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws-a", "agency", "reviewer");
        store.Resolve("sess-1", @"D:\ws-b", DateTime.UtcNow).agent.Should().Be("copilot");
    }

    [Fact]
    public void Path_matching_ignores_a_trailing_separator()
    {
        var store = new DeskAgentStore(null);
        store.RecordLaunch(@"D:\ws\", "agency", "reviewer");
        store.Resolve("sess-1", @"D:\ws", DateTime.UtcNow).agent.Should().Be("agency");
    }

    [Fact]
    public void Bindings_persist_across_instances()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "desk-agents-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var first = new DeskAgentStore(tmp);
            first.RecordLaunch(@"D:\ws", "agency", "reviewer");
            first.Resolve("sess-1", @"D:\ws", DateTime.UtcNow);   // bind + save

            var reopened = new DeskAgentStore(tmp);
            var r = reopened.Resolve("sess-1", @"D:\ws", DateTime.UtcNow);
            r.agent.Should().Be("agency");
            r.name.Should().Be("reviewer");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
