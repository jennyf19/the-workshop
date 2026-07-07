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
    public void Lists_only_folders_that_are_a_git_repo_and_carry_the_workshop_marker()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-list-" + Guid.NewGuid().ToString("N"));
        try
        {
            MakeDir(baseDir, "alpha", git: true, marker: true);          // a real workshop
            MakeDir(baseDir, "plain-clone", git: true, marker: false);   // e.g. the product repo — no marker
            MakeDir(baseDir, "marker-no-git", git: false, marker: true); // marker but not a repo

            var found = WorkshopLauncher.ListWorkshops(baseDir);

            found.Select(w => w.Name).Should().ContainSingle().Which.Should().Be("alpha");
            found[0].Dir.Should().Be(Path.Combine(baseDir, "alpha"));
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void ListWorkshops_returns_empty_for_a_missing_base_dir()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ws-missing-" + Guid.NewGuid().ToString("N"));
        WorkshopLauncher.ListWorkshops(missing).Should().BeEmpty();
    }

    private static void MakeDir(string baseDir, string name, bool git, bool marker)
    {
        var dir = Path.Combine(baseDir, name);
        Directory.CreateDirectory(dir);
        if (git) Directory.CreateDirectory(Path.Combine(dir, ".git"));
        if (marker) File.WriteAllText(Path.Combine(dir, "hands-up.md"), "");
    }
}
