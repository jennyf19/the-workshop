namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="WorkshopLauncher.ParseRepo"/> — turning what the operator
/// types in the "use existing" field (owner/name, a github URL, or owner/name.git)
/// into the (owner, name) pair used to clone.
/// </summary>
public class WorkshopLauncherTests
{
    [Theory]
    [InlineData("jennyf19/the-workshop", "jennyf19", "the-workshop")]
    [InlineData("https://github.com/jennyf19/the-workshop", "jennyf19", "the-workshop")]
    [InlineData("https://github.com/jennyf19/the-workshop.git", "jennyf19", "the-workshop")]
    [InlineData("  jeferrie_microsoft/Ember_workshop  ", "jeferrie_microsoft", "Ember_workshop")]
    public void Parses_owner_and_name(string input, string owner, string name)
    {
        var (o, n) = WorkshopLauncher.ParseRepo(input);
        o.Should().Be(owner);
        n.Should().Be(name);
    }

    [Theory]
    [InlineData("just-a-name")]   // no owner
    [InlineData("")]
    [InlineData("a/b/c")]         // too many segments
    [InlineData("/")]
    public void Rejects_input_without_a_clear_owner_and_name(string input)
    {
        var (o, n) = WorkshopLauncher.ParseRepo(input);
        o.Should().BeNull();
        n.Should().BeNull();
    }

    [Fact]
    public void Lists_git_repos_that_look_like_workshops_and_skips_plain_clones()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-list-" + Guid.NewGuid().ToString("N"));
        try
        {
            MakeRepo(baseDir, "scaffolded", git: true, handsUp: true, classroom: false, desks: true);
            MakeRepo(baseDir, "classroom-style", git: true, handsUp: false, classroom: true, desks: false); // e.g. Ember_workshop
            MakeRepo(baseDir, "plain-clone", git: true, handsUp: false, classroom: false, desks: false);    // product repo / random clone
            MakeRepo(baseDir, "not-a-repo", git: false, handsUp: false, classroom: true, desks: false);

            var found = WorkshopLauncher.ListWorkshops(baseDir).Select(w => w.Name).ToList();

            found.Should().BeEquivalentTo(new[] { "classroom-style", "scaffolded" });
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void ListWorkshops_returns_empty_for_a_missing_base_dir()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ws-missing-" + Guid.NewGuid().ToString("N"));
        WorkshopLauncher.ListWorkshops(missing).Should().BeEmpty();
    }

    private static void MakeRepo(string baseDir, string name, bool git, bool handsUp, bool classroom, bool desks)
    {
        var dir = Path.Combine(baseDir, name);
        Directory.CreateDirectory(dir);
        if (git) Directory.CreateDirectory(Path.Combine(dir, ".git"));
        if (handsUp) File.WriteAllText(Path.Combine(dir, "hands-up.md"), "");
        if (classroom) Directory.CreateDirectory(Path.Combine(dir, "classroom"));
        if (desks) Directory.CreateDirectory(Path.Combine(dir, "desks"));
    }
}
