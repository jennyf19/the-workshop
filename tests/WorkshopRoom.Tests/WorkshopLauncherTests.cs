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

    [Fact]
    public void PrepareDesk_scaffolds_the_desk_folder_and_leaves_existing_files_alone()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-desk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workshop = Path.Combine(baseDir, "shop");
            Directory.CreateDirectory(workshop);

            var deskDir = WorkshopLauncher.PrepareDesk(workshop, "alpha");

            deskDir.Should().Be(Path.Combine(workshop, "desks", "alpha"));
            File.Exists(Path.Combine(deskDir, "journal.md")).Should().BeTrue();
            File.Exists(Path.Combine(deskDir, "brief.md")).Should().BeTrue();
            File.Exists(Path.Combine(deskDir, "START-HERE.md")).Should().BeTrue();

            // idempotent: a second call must not clobber edits to the journal
            File.WriteAllText(Path.Combine(deskDir, "journal.md"), "my notes");
            WorkshopLauncher.PrepareDesk(workshop, "alpha");
            File.ReadAllText(Path.Combine(deskDir, "journal.md")).Should().Be("my notes");
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void PrepareDesk_defaults_a_blank_name_to_desk()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-desk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workshop = Path.Combine(baseDir, "shop");
            Directory.CreateDirectory(workshop);
            var deskDir = WorkshopLauncher.PrepareDesk(workshop, "  ");
            deskDir.Should().Be(Path.Combine(workshop, "desks", "desk"));
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void DeskOrientPrompt_points_straight_at_START_HERE_when_present()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-orient-" + Guid.NewGuid().ToString("N"));
        try
        {
            var deskDir = Path.Combine(baseDir, "desks", "alpha");
            Directory.CreateDirectory(deskDir);
            File.WriteAllText(Path.Combine(deskDir, "START-HERE.md"), "");

            var df = new WorkshopLauncher.DeskFolder("alpha", deskDir, "desks");
            WorkshopLauncher.DeskOrientPrompt(df)
                .Should().Be("read ./desks/alpha/START-HERE.md, then get going");
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void DeskOrientPrompt_names_only_the_orientation_files_that_exist()
    {
        // a classroom-style desk (e.g. Ember_workshop): BENCH.md + journal.md, no README, no START-HERE
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-orient-" + Guid.NewGuid().ToString("N"));
        try
        {
            var deskDir = Path.Combine(baseDir, "classroom", "workshop-product");
            Directory.CreateDirectory(deskDir);
            File.WriteAllText(Path.Combine(deskDir, "BENCH.md"), "");
            File.WriteAllText(Path.Combine(deskDir, "journal.md"), "");

            var df = new WorkshopLauncher.DeskFolder("workshop-product", deskDir, "classroom");
            var prompt = WorkshopLauncher.DeskOrientPrompt(df);

            prompt.Should().Contain("./classroom/workshop-product/BENCH.md");
            prompt.Should().Contain("./classroom/workshop-product/journal.md");
            prompt.Should().NotContain("README.md");    // don't send it after a file that isn't there
            prompt.Should().NotContain("if it exists");  // the dead-end phrasing that caused the bug
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
    }

    [Fact]
    public void DeskOrientPrompt_falls_back_to_explore_when_the_folder_has_no_known_files()
    {
        // the exact reported bug: classroom/workshop has neither README nor START-HERE,
        // so the old prompt sent the desk to a README that isn't there.
        var baseDir = Path.Combine(Path.GetTempPath(), "ws-orient-" + Guid.NewGuid().ToString("N"));
        try
        {
            var deskDir = Path.Combine(baseDir, "classroom", "workshop");
            Directory.CreateDirectory(deskDir);

            var df = new WorkshopLauncher.DeskFolder("workshop", deskDir, "classroom");
            var prompt = WorkshopLauncher.DeskOrientPrompt(df);

            prompt.Should().Contain("orient yourself at ./classroom/workshop/");
            prompt.Should().NotContain("if it exists");  // never claim a specific file is there
        }
        finally { try { Directory.Delete(baseDir, recursive: true); } catch { } }
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
