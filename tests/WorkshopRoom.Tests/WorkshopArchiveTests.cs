namespace WorkshopRoom.Tests;

/// <summary>
/// A workshop can be archived (hidden from the board) and restored. The state is
/// persisted to a JSON file so it survives a restart, mirrors the closed-desks
/// store, and is case-insensitive (Windows paths). The workshop folder itself is
/// never touched — archiving is board visibility only.
/// </summary>
public class WorkshopArchiveTests
{
    private static WorkshopArchive Make(string dir) =>
        new(Path.Combine(dir, "archived-workshops.json"));

    [Fact]
    public void Archive_then_restore_toggles_membership()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var a = Make(tmp.FullName);
            a.IsArchived(@"D:\ws").Should().BeFalse();

            a.Archive(@"D:\ws");
            a.IsArchived(@"D:\ws").Should().BeTrue();

            a.Restore(@"D:\ws");
            a.IsArchived(@"D:\ws").Should().BeFalse();
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void Archive_persists_across_instances()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            Make(tmp.FullName).Archive(@"D:\keepme");

            // A fresh instance reading the same file still sees it.
            Make(tmp.FullName).IsArchived(@"D:\keepme").Should().BeTrue();
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void Archive_is_idempotent()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var a = Make(tmp.FullName);
            a.Archive(@"D:\ws");
            a.Archive(@"D:\ws");

            a.Archived().Should().ContainSingle().Which.Should().Be(@"D:\ws");
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void Archived_is_case_insensitive_for_windows_paths()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var a = Make(tmp.FullName);
            a.Archive(@"D:\Ember_workshop");

            a.IsArchived(@"d:\ember_workshop").Should().BeTrue();
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_dirs_are_ignored(string dir)
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var a = Make(tmp.FullName);
            a.Archive(dir);
            a.Archived().Should().BeEmpty();
        }
        finally { tmp.Delete(recursive: true); }
    }
}
