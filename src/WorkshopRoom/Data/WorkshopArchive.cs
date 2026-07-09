using System.Text.Json;

namespace WorkshopRoom.Data;

// Tracks which workshops the operator has archived: hidden from the board but
// kept whole on disk. The workshop folder — its bench, desks, and journals — is
// the durable state, so an archived workshop restores intact. Persisted as a
// JSON array of workshop directories, mirroring closed-desks.json for desks.
public class WorkshopArchive
{
    private readonly string _path;
    private readonly object _lock = new();

    public WorkshopArchive(string path) { _path = path; }

    // The set of archived workshop directories (case-insensitive, since paths
    // on Windows are).
    public HashSet<string> Archived()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(_path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var e in doc.RootElement.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.String && e.GetString() is string s)
                            set.Add(s);
            }
        }
        catch { /* the archive list is optional */ }
        return set;
    }

    public bool IsArchived(string dir) => !string.IsNullOrWhiteSpace(dir) && Archived().Contains(dir);

    // Archive a workshop by directory. Idempotent.
    public void Archive(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        lock (_lock)
        {
            var set = Archived();
            if (set.Add(dir)) Save(set);
        }
    }

    // Bring an archived workshop back to the board. Idempotent.
    public void Restore(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        lock (_lock)
        {
            var set = Archived();
            if (set.Remove(dir)) Save(set);
        }
    }

    private void Save(HashSet<string> set)
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(set)); }
        catch { /* best effort — a failed write just means it re-shows next load */ }
    }
}
