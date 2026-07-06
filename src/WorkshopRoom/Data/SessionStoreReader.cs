using System.Text.Json;

namespace WorkshopRoom.Data;

// A live snapshot of the room read straight from the Copilot CLI session-state
// on disk. This is the in-app port of the PowerShell exporter: the room reads
// the desks itself and refreshes, instead of waiting for a manual export.
public record LiveSnapshot(
    Artifact Artifact, List<Desk> Desks, List<BenchTurn> Bench,
    List<HandsUp> HandsUp, List<Handoff> Handoffs, Health Health, Pulse Pulse,
    string UsageAsOf);

public sealed class SessionStoreReader
{
    private readonly string _root;        // ~/.copilot/session-state
    private readonly string _usageCache;  // usage-cache.json (cloud tokens/AIC, periodic sync)
    private readonly string _namesPath;   // desk-names.json (operator overrides, optional)
    private readonly string _resolvedPath; // handsup-resolved.json (operator-dismissed hands)
    private readonly int _windowHours;
    private readonly int _maxDesks;

    private readonly object _lock = new();
    private LiveSnapshot? _cache;
    private DateTime _lastScan = DateTime.MinValue;

    // parse cache: session id -> (events.jsonl mtime, parsed). Re-parsed only when the file changes.
    private readonly Dictionary<string, (DateTime mtime, ParsedSession parsed)> _perSession = new();

    public SessionStoreReader(string root, string usageCache, string namesPath, string resolvedPath, int windowHours = 12, int maxDesks = 10)
    {
        _root = root;
        _usageCache = usageCache;
        _namesPath = namesPath;
        _resolvedPath = resolvedPath;
        _windowHours = windowHours;
        _maxDesks = maxDesks;
    }

    public LiveSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            if (_cache is not null && (DateTime.UtcNow - _lastScan).TotalSeconds < 2.5) return _cache;
            _cache = Scan();
            _lastScan = DateTime.UtcNow;
            return _cache;
        }
    }

    private LiveSnapshot Scan()
    {
        var usage = LoadUsage(out var asOf);
        var (namesById, namesByCwd) = LoadNames();
        var resolved = LoadResolved();
        var desks = new List<Desk>();
        var bench = new List<BenchTurn>();
        var handsup = new List<HandsUp>();

        if (Directory.Exists(_root))
        {
            var recent = new DirectoryInfo(_root).GetDirectories()
                .Select(d => (dir: d, ev: new FileInfo(Path.Combine(d.FullName, "events.jsonl"))))
                .Where(x => x.ev.Exists && (DateTime.UtcNow - x.ev.LastWriteTimeUtc).TotalHours <= _windowHours)
                .OrderBy(x => x.ev.LastWriteTimeUtc) // ascending: most-recent desk lands last (latest highlight)
                .ToList();
            if (recent.Count > _maxDesks) recent = recent.Skip(recent.Count - _maxDesks).ToList();

            int seq = 0;
            foreach (var (dir, ev) in recent)
            {
                ParsedSession ps;
                try { ps = ParseSession(dir.FullName, ev); }
                catch { continue; }

                long tin = 0, tout = 0, aic = 0;
                if (ps.CloudId is not null && usage.TryGetValue(ps.CloudId, out var u)) { tin = u.tin; tout = u.tout; aic = u.aic; }

                var lastSeen = ev.LastWriteTimeUtc;
                var mins = (DateTime.UtcNow - lastSeen).TotalMinutes;
                var status = mins <= 10 ? "active" : mins <= 120 ? "idle" : "quiet";

                var fullId = dir.Name;
                var shortId = fullId.Length >= 8 ? fullId[..8] : fullId;
                var name = ResolveName(shortId, ps.Cwd, ps.UserNamed, ps.RawName, namesById, namesByCwd);
                var consolePid = OpenConsolePid(dir.FullName);
                var sync = GitStatus.StateFor(ps.Cwd);

                desks.Add(new Desk(0, name, "live", ps.Model, status, ps.Note, lastSeen, tin, tout, aic, fullId, ps.Cwd, consolePid, sync));

                if (!string.IsNullOrWhiteSpace(ps.LastUser))
                    bench.Add(new BenchTurn(0, ++seq, name, "claim", Trunc(ps.LastUser!, 200), null, name, lastSeen));
                if (!string.IsNullOrWhiteSpace(ps.LastAssistant))
                    bench.Add(new BenchTurn(0, ++seq, name, "note", Trunc(ps.LastAssistant!, 200), null, name, lastSeen));

                var question = OperatorQuestion(ps.PendingAsk, ps.LastAssistant, mins);
                if (question is not null)
                {
                    var display = Trunc(question, 200);
                    if (!resolved.Contains(ResolveKey(name, display)))
                        handsup.Add(new HandsUp(0, display, name, "open", lastSeen));
                }
            }
        }

        var handoffs = new List<Handoff>();
        var artifact = new Artifact(0, "", desks.Count > 0 ? $"{desks.Count} live desks" : "no active desks in the last " + _windowHours + "h");
        var health = Metrics.ComputeHealth(desks, bench, handsup, handoffs);
        var pulse = Metrics.ComputePulse(desks, bench, handsup, handoffs);
        return new LiveSnapshot(artifact, desks, bench, handsup, handoffs, health, pulse, asOf);
    }

    private ParsedSession ParseSession(string dir, FileInfo ev)
    {
        var id = Path.GetFileName(dir);
        if (_perSession.TryGetValue(id, out var cached) && cached.mtime == ev.LastWriteTimeUtc)
            return cached.parsed;

        string rawName = "";
        string model = "-";
        string? cloudId = null;
        string cwd = "";
        bool userNamed = false;
        string note = "";

        var wy = Path.Combine(dir, "workspace.yaml");
        if (File.Exists(wy))
        {
            foreach (var raw in File.ReadLines(wy))
            {
                var t = raw.Trim();
                if (t.StartsWith("name:")) { var v = t["name:".Length..].Trim(); if (v.Length > 0) rawName = v; }
                else if (t.StartsWith("mc_session_id:")) cloudId = t["mc_session_id:".Length..].Trim();
                else if (t.StartsWith("cwd:")) { var v = t["cwd:".Length..].Trim(); if (v.Length > 0) cwd = v; }
                else if (t.StartsWith("user_named:")) userNamed = t["user_named:".Length..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        string? firstUser = null, lastUser = null, lastAssistant = null;
        string? pendingAskId = null, pendingAskQuestion = null;
        foreach (var line in File.ReadLines(ev.FullName))
        {
            if (line.Contains("\"user.message\""))
            {
                var c = ExtractContent(line);
                if (c is not null) { firstUser ??= c; lastUser = c; }
                pendingAskId = null; pendingAskQuestion = null; // a reply unblocks the desk
            }
            else if (line.Contains("\"assistant.message\""))
            {
                var c = ExtractContent(line);
                if (c is not null) lastAssistant = c;
            }
            else if (line.Contains("\"tool.execution_start\"") && line.Contains("ask_user"))
            {
                var ask = TryAskUserStart(line);
                if (ask is not null) { pendingAskId = ask.Value.id; pendingAskQuestion = ask.Value.question; }
            }
            else if (line.Contains("\"tool.execution_complete\"") && pendingAskId is not null)
            {
                if (ToolCompleteId(line) == pendingAskId) { pendingAskId = null; pendingAskQuestion = null; }
            }
        }
        if (firstUser is not null) note = Trunc(StripReminder(firstUser), 90);
        else if (cwd.Length > 0) note = cwd;

        var parsed = new ParsedSession(rawName, cloudId, model, cwd, userNamed, note, lastUser,
            lastAssistant is null ? null : StripReminder(lastAssistant),
            pendingAskQuestion is null ? null : StripReminder(pendingAskQuestion));
        _perSession[id] = (ev.LastWriteTimeUtc, parsed);
        return parsed;
    }

    private static string? ExtractContent(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
                return content.GetString();
        }
        catch { /* not a parseable event line */ }
        return null;
    }

    // ask_user logs a tool.execution_start with toolName "ask_user" and the
    // question in arguments.question; the matching tool.execution_complete only
    // fires once the operator answers, so a start with no later complete means
    // the desk is blocked on a reply right now.
    private static (string? id, string? question)? TryAskUserStart(string line)
    {
        try
        {
        using var doc = JsonDocument.Parse(line);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (!data.TryGetProperty("toolName", out var tn) || tn.GetString() != "ask_user") return null;
        string? id = data.TryGetProperty("toolCallId", out var tc) ? tc.GetString() : null;
        string? q = data.TryGetProperty("arguments", out var args)
                    && args.TryGetProperty("question", out var qq)
                    && qq.ValueKind == JsonValueKind.String
            ? qq.GetString() : null;
        return (id, q);
        }
        catch { return null; }
    }

    private static string? ToolCompleteId(string line)
    {
        try
        {
        using var doc = JsonDocument.Parse(line);
        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("toolCallId", out var tc))
            return tc.GetString();
        }
        catch { /* not parseable */ }
        return null;
    }

    private static string StripReminder(string s)
    {
        var i = s.IndexOf("<system_reminder", StringComparison.Ordinal);
        if (i > 0) s = s[..i];
        return s.Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private Dictionary<string, (long tin, long tout, long aic)> LoadUsage(out string asOf)
    {
        asOf = "never synced";
        var map = new Dictionary<string, (long, long, long)>();
        try
        {
            if (File.Exists(_usageCache))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_usageCache));
                var root = doc.RootElement;
                if (root.TryGetProperty("asOf", out var a) && a.ValueKind == JsonValueKind.String) asOf = a.GetString() ?? asOf;
                if (root.TryGetProperty("byCloudId", out var by))
                    foreach (var p in by.EnumerateObject())
                    {
                        long Get(string k) => p.Value.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
                        map[p.Name] = (Get("tokensIn"), Get("tokensOut"), Get("aic"));
                    }
            }
        }
        catch { /* cache optional */ }
        return map;
    }

    // Desk name resolution: operator override (by id or cwd) > an explicitly
    // named session > the working-folder name > the auto session name > short id.
    // This is why a desk shows "code-review" or "The_Workshop" instead of the
    // auto-generated "Create New Project".
    internal static string ResolveName(string shortId, string cwd, bool userNamed, string rawName,
        Dictionary<string, string> byId, Dictionary<string, string> byCwd)
    {
        if (byId.TryGetValue(shortId, out var n1)) return n1;
        if (!string.IsNullOrEmpty(cwd) && byCwd.TryGetValue(cwd, out var n2)) return n2;
        if (userNamed && !string.IsNullOrWhiteSpace(rawName)) return rawName;
        var leaf = MeaningfulLeaf(cwd);
        if (leaf is not null) return leaf;
        if (!string.IsNullOrWhiteSpace(rawName)) return rawName;
        return "session " + shortId;
    }

    // Last path segment of cwd, unless cwd is a bare drive root (e.g. "D:\").
    internal static string? MeaningfulLeaf(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return null;
        var t = cwd.TrimEnd('\\', '/');
        if (t.Length <= 2 || t.EndsWith(":")) return null;
        int idx = t.LastIndexOfAny(new[] { '\\', '/' });
        var leaf = idx >= 0 ? t[(idx + 1)..] : t;
        return string.IsNullOrWhiteSpace(leaf) || leaf.EndsWith(":") ? null : leaf;
    }

    // Whether a desk is waiting on the operator, and the question if so. Hard
    // signal: an unanswered ask_user (the desk is blocked on a reply right now,
    // however long ago). Soft signal: the desk has gone idle and its last
    // assistant message ended on a question. Null means the room is settling its
    // own work and nothing is queued for the operator.
    internal static string? OperatorQuestion(string? pendingAsk, string? lastAssistant, double idleMinutes)
    {
        if (!string.IsNullOrWhiteSpace(pendingAsk)) return pendingAsk!.Trim();
        if (idleMinutes > 10 && !string.IsNullOrWhiteSpace(lastAssistant))
        {
            var t = lastAssistant!.TrimEnd();
            if (t.EndsWith('?')) return t;
        }
        return null;
    }

    // A stable, desk-scoped identity for a hand, so the operator can dismiss one
    // and have it stay dismissed across sweeps — until that desk asks something
    // new (a different question yields a different key, which re-raises).
    internal static string ResolveKey(string desk, string question) => desk + "\u0000" + question;

    // Hands the operator has explicitly resolved; the sweep suppresses any
    // derived hand whose key matches.
    private HashSet<string> LoadResolved()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (File.Exists(_resolvedPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_resolvedPath));
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var e in doc.RootElement.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.String) set.Add(e.GetString()!);
            }
        }
        catch { /* dismissals are optional */ }
        return set;
    }

    // Operator action: dismiss a hand. Persists the dismissal and drops the
    // cached snapshot so the next read re-scans without it.
    public void Resolve(string desk, string question)
    {
        var set = LoadResolved();
        if (set.Add(ResolveKey(desk, question)))
        {
            try { File.WriteAllText(_resolvedPath, JsonSerializer.Serialize(set)); }
            catch { /* best effort */ }
        }
        lock (_lock) { _cache = null; }
    }

    private (Dictionary<string, string> byId, Dictionary<string, string> byCwd) LoadNames()
    {
        var byId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byCwd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(_namesPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_namesPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("byId", out var bi))
                    foreach (var p in bi.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String) byId[p.Name] = p.Value.GetString()!;
                if (root.TryGetProperty("byCwd", out var bc))
                    foreach (var p in bc.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String) byCwd[p.Name] = p.Value.GetString()!;
            }
        }
        catch { /* overrides optional */ }
        return (byId, byCwd);
    }

    // A live session drops an inuse.<pid>.lock in its folder; its presence means
    // the desk is currently open in a console. Returns that pid, or 0 if closed.
    private static int OpenConsolePid(string dir)
    {
        try
        {
            var lockFile = Directory.EnumerateFiles(dir, "inuse.*.lock").FirstOrDefault();
            if (lockFile is null) return 0;
            var pidStr = Path.GetFileName(lockFile).Replace("inuse.", "").Replace(".lock", "");
            return int.TryParse(pidStr, out var pid) ? pid : 0;
        }
        catch { return 0; }
    }

    private static string Trunc(string s, int n) => s.Length > n ? s[..n] + " ..." : s;

    private record ParsedSession(string RawName, string? CloudId, string Model, string Cwd, bool UserNamed, string Note, string? LastUser, string? LastAssistant, string? PendingAsk);
}
