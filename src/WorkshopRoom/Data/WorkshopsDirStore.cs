using System.Text.Json;

namespace WorkshopRoom.Data;

// Persists the operator-chosen "workshops folder" — the directory the board
// scans for workshops — so the choice survives restarts. Without this, the base
// dir falls back to the drive root, which almost never holds workshop-marked
// folders, so a fresh run shows an empty board (issue: "a page with the cairn
// instead of the workshop"). The WORKSHOP_DIR environment variable, when set,
// takes precedence and this store is not consulted.
public sealed class WorkshopsDirStore
{
    private readonly string? _path;
    private readonly object _lock = new();

    // path == null keeps the setting in memory only (used by tests).
    public WorkshopsDirStore(string? path) => _path = path;

    // The persisted workshops folder, or null when none has been chosen.
    public string? Get()
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return _mem;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                if (doc.RootElement.TryGetProperty("dir", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    var dir = d.GetString();
                    return string.IsNullOrWhiteSpace(dir) ? null : dir;
                }
            }
            catch { /* store is optional; treat a bad file as unset */ }
            return null;
        }
    }

    // Persist the chosen workshops folder.
    public void Save(string dir)
    {
        lock (_lock)
        {
            _mem = string.IsNullOrWhiteSpace(dir) ? null : dir.Trim();
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                var payload = new { dir = _mem ?? "" };
                File.WriteAllText(_path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* best effort */ }
        }
    }

    private string? _mem;
}
