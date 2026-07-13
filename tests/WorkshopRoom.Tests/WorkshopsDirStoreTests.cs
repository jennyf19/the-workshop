using WorkshopRoom.Data;

namespace WorkshopRoom.Tests;

/// <summary>
/// The workshops-folder setting is persisted so the board remembers where to
/// scan across restarts, instead of falling back to the drive root (which shows
/// an empty "cairn page"). These tests pin the save/get roundtrip, persistence
/// across instances, and the in-memory (no-path) mode used by callers without a
/// state dir.
/// </summary>
public class WorkshopsDirStoreTests
{
    [Fact]
    public void Get_is_null_when_nothing_saved()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "workshops-dir.json");
        var store = new WorkshopsDirStore(path);
        store.Get().Should().BeNull();
    }

    [Fact]
    public void Save_then_get_roundtrips_and_persists_across_instances()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "workshops-dir.json");
            new WorkshopsDirStore(path).Save(@"D:\repos");

            // A fresh instance reads the persisted value back from disk.
            new WorkshopsDirStore(path).Get().Should().Be(@"D:\repos");
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Save_trims_and_blank_clears()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "workshops-dir.json");
            var store = new WorkshopsDirStore(path);

            store.Save("  C:\\src  ");
            store.Get().Should().Be("C:\\src");

            store.Save("   ");
            new WorkshopsDirStore(path).Get().Should().BeNull();
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Null_path_keeps_setting_in_memory_only()
    {
        var store = new WorkshopsDirStore(null);
        store.Get().Should().BeNull();
        store.Save(@"C:\here");
        store.Get().Should().Be(@"C:\here");
    }
}
