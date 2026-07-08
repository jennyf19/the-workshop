using System.Text.Json;

namespace WorkshopRoom.Data;

// Reads agent signals from .signals/ directories inside desk folders.
// Agents (GHCP CLI or Claude Code) emit signals by writing a JSON file into
// their desk's .signals/ folder. The dashboard picks them up on the next 3s
// refresh cycle. Follows the agent-signals protocol from agentic-devops.
public static class SignalReader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Scans all desk folders (from all workshops) for .signals/ directories
    /// and returns the most recent signal per desk.
    /// </summary>
    public static List<AgentSignal> ReadLatestSignals(IEnumerable<string> workshopDirs)
    {
        var signals = new List<AgentSignal>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var wsDir in workshopDirs)
        {
            ScanDeskSignals(wsDir, "desks", signals, seen);
            ScanDeskSignals(wsDir, "classroom", signals, seen);
        }

        return signals;
    }

    /// <summary>
    /// Returns the single most recent signal for a given desk name, or null.
    /// </summary>
    public static AgentSignal? LatestForDesk(List<AgentSignal> allSignals, string deskName) =>
        allSignals
            .Where(s => s.DeskName.Equals(deskName, StringComparison.OrdinalIgnoreCase))
            .MaxBy(s => s.EmittedAt);

    private static void ScanDeskSignals(string workshopDir, string subdir,
        List<AgentSignal> results, HashSet<string> seenDesks)
    {
        var parent = Path.Combine(workshopDir, subdir);
        if (!Directory.Exists(parent)) return;

        try
        {
            foreach (var deskDir in Directory.EnumerateDirectories(parent))
            {
                var deskName = Path.GetFileName(deskDir);
                if (deskName.StartsWith('.')) continue;

                var sigDir = Path.Combine(deskDir, ".signals");
                if (!Directory.Exists(sigDir)) continue;

                // Only the most recent signal per desk matters for the dashboard
                var latest = MostRecentSignalFile(sigDir);
                if (latest is null) continue;

                var key = $"{workshopDir}|{deskName}";
                if (!seenDesks.Add(key)) continue; // first workshop wins for duplicate desk names

                var signal = ParseSignalFile(latest, deskName);
                if (signal is not null) results.Add(signal);
            }
        }
        catch { /* skip unreadable directories */ }
    }

    private static string? MostRecentSignalFile(string sigDir)
    {
        try
        {
            return Directory.EnumerateFiles(sigDir, "*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    private static AgentSignal? ParseSignalFile(string path, string deskName)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            var root = doc.RootElement;

            var signalType = GetString(root, "signal_type") ?? "execution";
            var agentName = GetString(root, "agent_name") ?? deskName;

            // Self-assessment
            int confidence = 0, accuracy = 0, completeness = 0;
            if (root.TryGetProperty("self_assessment", out var sa))
            {
                confidence = GetInt(sa, "confidence");
                accuracy = GetInt(sa, "accuracy");
                completeness = GetInt(sa, "completeness");
            }

            // Patterns
            string whatWorked = "", whatWasHard = "", skillGap = "";
            if (root.TryGetProperty("patterns", out var pat))
            {
                whatWorked = GetString(pat, "what_worked") ?? "";
                whatWasHard = GetString(pat, "what_was_hard") ?? "";
                skillGap = GetString(pat, "skill_gap") ?? "";
            }

            // Escalation (only for escalation signals)
            string? escalationReason = null, escalationBlocked = null, recommendation = null;
            if (root.TryGetProperty("escalation", out var esc))
            {
                escalationReason = GetString(esc, "reason");
                escalationBlocked = GetString(esc, "blocked_on");
                recommendation = GetString(esc, "recommendation");
            }

            var emitted = File.GetLastWriteTimeUtc(path);

            return new AgentSignal(
                signalType, deskName, agentName,
                confidence, accuracy, completeness,
                whatWorked, whatWasHard, skillGap,
                escalationReason, escalationBlocked, recommendation,
                emitted, path);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : 0;
}
