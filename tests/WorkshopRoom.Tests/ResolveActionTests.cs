namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for the operator "resolve" action on <see cref="SessionStoreReader"/>.
/// Hands-up are derived from live session-state, so a dismissal has to be
/// persisted and keyed to (desk, question): the same hand stays dismissed across
/// sweeps, but a new question from that desk produces a different key and
/// re-raises.
/// </summary>
public class ResolveActionTests
{
    [Fact]
    public void Resolve_key_is_stable_desk_scoped_and_question_scoped()
    {
        var key = SessionStoreReader.ResolveKey("code-review", "ship it?");

        SessionStoreReader.ResolveKey("code-review", "ship it?").Should().Be(key);     // stable
        SessionStoreReader.ResolveKey("payments-api", "ship it?").Should().NotBe(key);   // desk-scoped
        SessionStoreReader.ResolveKey("code-review", "something else?").Should().NotBe(key); // question-scoped
    }

    [Fact]
    public void Resolving_persists_the_dismissal()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var resolvedPath = Path.Combine(dir.FullName, "handsup-resolved.json");
            var reader = new SessionStoreReader(
                root: Path.Combine(dir.FullName, "no-sessions"),
                usageCache: Path.Combine(dir.FullName, "usage.json"),
                namesPath: Path.Combine(dir.FullName, "names.json"),
                resolvedPath: resolvedPath,
                closedPath: Path.Combine(dir.FullName, "closed-desks.json"));

            reader.Resolve("code-review", "ship it?");

            File.Exists(resolvedPath).Should().BeTrue();
            var saved = File.ReadAllText(resolvedPath);
            saved.Should().Contain("code-review");
            saved.Should().Contain("ship it");
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Resolving_is_idempotent_and_a_fresh_reader_sees_the_dismissal()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var resolvedPath = Path.Combine(dir.FullName, "handsup-resolved.json");
            SessionStoreReader Make() => new(
                root: Path.Combine(dir.FullName, "no-sessions"),
                usageCache: Path.Combine(dir.FullName, "usage.json"),
                namesPath: Path.Combine(dir.FullName, "names.json"),
                resolvedPath: resolvedPath,
                closedPath: Path.Combine(dir.FullName, "closed-desks.json"));

            Make().Resolve("code-review", "ship it?");
            Make().Resolve("code-review", "ship it?");   // idempotent — same key, no duplicate

            // A second, independent reader still has the dismissal on disk.
            var key = SessionStoreReader.ResolveKey("code-review", "ship it?");
            var saved = File.ReadAllText(resolvedPath);
            System.Text.RegularExpressions.Regex.Matches(saved, "code-review").Count.Should().Be(1);
            saved.Should().Contain("ship it");
            key.Should().NotBeNullOrEmpty();
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Closing_a_desk_persists_the_closure_by_session_id()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var closedPath = Path.Combine(dir.FullName, "closed-desks.json");
            SessionStoreReader Make() => new(
                root: Path.Combine(dir.FullName, "no-sessions"),
                usageCache: Path.Combine(dir.FullName, "usage.json"),
                namesPath: Path.Combine(dir.FullName, "names.json"),
                resolvedPath: Path.Combine(dir.FullName, "handsup-resolved.json"),
                closedPath: closedPath);

            Make().Close("session-abc");
            Make().Close("session-abc");   // idempotent — same id, no duplicate

            File.Exists(closedPath).Should().BeTrue();
            var saved = File.ReadAllText(closedPath);
            saved.Should().Contain("session-abc");
            System.Text.RegularExpressions.Regex.Matches(saved, "session-abc").Count.Should().Be(1);
        }
        finally { dir.Delete(recursive: true); }
    }
}
