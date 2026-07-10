using System.Text.Json;

namespace WorkshopRoom.Data;

// Records which agent CLI launched a desk, so an Agency desk can be told apart
// from a Copilot one after launch. This is necessary because Agency drives
// Copilot under the hood and aligns its session id onto Copilot's on-disk id —
// so on disk the two are indistinguishable (issue #2), and the reader would
// otherwise resume every desk as bare Copilot.
//
// The room can't know the session id at launch (Agency mints it), so a launch
// is recorded as a pending entry keyed by working directory and bound to the
// first new session that appears in that directory afterwards. Bindings are
// then stable by session id. Copilot launches aren't recorded — bare Copilot
// resume is already the correct default.
public sealed class DeskAgentStore
{
    private readonly string? _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, Binding> _byId = new(StringComparer.Ordinal);
    private readonly List<Pending> _pending = new();
    private bool _loaded;

    private sealed record Binding(string Agent, string? Name);

    private sealed class Pending
    {
        public string Cwd = "";
        public string Agent = "";
        public string? Name;
        public DateTime Ts;
    }

    // A pending launch older than this with no matching session is dropped: the
    // launch was cancelled or failed, and a stale entry must not capture an
    // unrelated later session in the same folder.
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(15);

    // path == null keeps everything in memory only (used by tests).
    public DeskAgentStore(string? path) => _path = path;

    // Record that `agentKey` launched a desk in `cwd` (optionally named). Only
    // non-Copilot launches are kept; Copilot is the default resume shape.
    public void RecordLaunch(string cwd, string agentKey, string? name)
    {
        if (string.IsNullOrWhiteSpace(cwd) || string.IsNullOrWhiteSpace(agentKey)) return;
        if (string.Equals(agentKey, AgentClis.Copilot.Key, StringComparison.OrdinalIgnoreCase)) return;
        lock (_lock)
        {
            Load();
            Prune();
            _pending.Add(new Pending
            {
                Cwd = Norm(cwd),
                Agent = agentKey,
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                Ts = DateTime.UtcNow,
            });
            Save();
        }
    }

    // Resolve the launching agent (and the operator-chosen name, if any) for a
    // session, binding the oldest matching pending launch the first time the
    // session is seen. Defaults to Copilot when nothing was recorded.
    public (string agent, string? name) Resolve(string sessionId, string cwd, DateTime? createdUtc)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return (AgentClis.Copilot.Key, null);
        lock (_lock)
        {
            Load();
            if (_byId.TryGetValue(sessionId, out var bound)) return (bound.Agent, bound.Name);

            Prune();
            var cwdN = Norm(cwd);
            Pending? match = null;
            foreach (var p in _pending)
            {
                if (!string.Equals(p.Cwd, cwdN, StringComparison.OrdinalIgnoreCase)) continue;
                // The session must not predate the launch (allow a little clock skew).
                if (createdUtc is not null && createdUtc.Value < p.Ts.AddSeconds(-10)) continue;
                if (match is null || p.Ts < match.Ts) match = p;   // oldest pending, FIFO
            }
            if (match is null) return (AgentClis.Copilot.Key, null);

            var binding = new Binding(match.Agent, match.Name);
            _byId[sessionId] = binding;
            _pending.Remove(match);
            Save();
            return (binding.Agent, binding.Name);
        }
    }

    private void Prune()
    {
        var cutoff = DateTime.UtcNow - PendingTtl;
        _pending.RemoveAll(p => p.Ts < cutoff);
    }

    private static string Norm(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
        catch { return (path ?? "").TrimEnd('\\', '/'); }
    }

    private void Load()
    {
        if (_loaded) return;
        _loaded = true;
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            var root = doc.RootElement;
            if (root.TryGetProperty("byId", out var byId) && byId.ValueKind == JsonValueKind.Object)
                foreach (var p in byId.EnumerateObject())
                {
                    var agent = p.Value.TryGetProperty("agent", out var a) ? a.GetString() : null;
                    var name = p.Value.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    if (!string.IsNullOrEmpty(agent)) _byId[p.Name] = new Binding(agent!, name);
                }
            if (root.TryGetProperty("pending", out var pend) && pend.ValueKind == JsonValueKind.Array)
                foreach (var e in pend.EnumerateArray())
                {
                    var cwd = e.TryGetProperty("cwd", out var c) ? c.GetString() : null;
                    var agent = e.TryGetProperty("agent", out var a) ? a.GetString() : null;
                    var name = e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    var ts = e.TryGetProperty("ts", out var t) && t.TryGetDateTime(out var dt)
                        ? dt.ToUniversalTime() : DateTime.MinValue;
                    if (!string.IsNullOrEmpty(cwd) && !string.IsNullOrEmpty(agent))
                        _pending.Add(new Pending { Cwd = cwd!, Agent = agent!, Name = name, Ts = ts });
                }
        }
        catch { /* store is optional; treat a bad file as empty */ }
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(_path)) return;
        try
        {
            var payload = new
            {
                byId = _byId.ToDictionary(kv => kv.Key, kv => new { agent = kv.Value.Agent, name = kv.Value.Name }),
                pending = _pending.Select(p => new { cwd = p.Cwd, agent = p.Agent, name = p.Name, ts = p.Ts }).ToList(),
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
