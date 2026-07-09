namespace WorkshopRoom.Tests;

/// <summary>
/// A mini-workshop is the lightweight on-ramp: a local folder with a workshop.md
/// brief and a desks/ folder, no git and no GitHub repo. These tests pin the
/// scaffold shape, the validation rules, and that the room lists it (flagged as
/// mini) so a desk can be opened in it.
/// </summary>
public class MiniWorkshopTests
{
    [Fact]
    public void NewMiniWorkshop_creates_local_folder_with_brief_and_desks_no_git()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var result = WorkshopLauncher.NewMiniWorkshop("scratch", baseDir.FullName);

            result.Ok.Should().BeTrue(result.Message);
            result.Dir.Should().NotBeNull();
            var dir = result.Dir!;
            File.Exists(Path.Combine(dir, "workshop.md")).Should().BeTrue();
            Directory.Exists(Path.Combine(dir, "desks")).Should().BeTrue();
            Directory.Exists(Path.Combine(dir, ".git")).Should().BeFalse();   // no git — that's the point
            File.ReadAllText(Path.Combine(dir, "workshop.md")).Should().Contain("scratch");
        }
        finally { baseDir.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    public void NewMiniWorkshop_rejects_blank_and_spaced_names(string name)
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var result = WorkshopLauncher.NewMiniWorkshop(name, baseDir.FullName);
            result.Ok.Should().BeFalse();
            result.Dir.Should().BeNull();
        }
        finally { baseDir.Delete(recursive: true); }
    }

    [Fact]
    public void NewMiniWorkshop_refuses_a_nonempty_existing_dir()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var clash = Path.Combine(baseDir.FullName, "taken");
            Directory.CreateDirectory(clash);
            File.WriteAllText(Path.Combine(clash, "something.txt"), "x");

            var result = WorkshopLauncher.NewMiniWorkshop("taken", baseDir.FullName);

            result.Ok.Should().BeFalse();
            result.Message.Should().Contain("isn't empty");
        }
        finally { baseDir.Delete(recursive: true); }
    }

    [Fact]
    public void ListWorkshops_lists_a_mini_workshop_flagged_as_mini()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            WorkshopLauncher.NewMiniWorkshop("mini-one", baseDir.FullName);

            var listed = WorkshopLauncher.ListWorkshops(baseDir.FullName);

            var mini = listed.Should().ContainSingle(w => w.Name == "mini-one").Subject;
            mini.IsMini.Should().BeTrue();
        }
        finally { baseDir.Delete(recursive: true); }
    }
}
