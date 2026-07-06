using System.Diagnostics;

namespace WorkshopRoom.Data;

// Lightweight, cached git state per desk folder:
//   "wip"          uncommitted changes in this folder
//   "unpushed:N"   N commits ahead of origin/main (not pushed)
//   "behind:N"     N commits behind origin/main (best-effort; no fetch is done)
//   "synced"       clean and up to date
//   ""             not a git repo
// Cached ~45s so the per-scan reader doesn't spawn git constantly. No fetch is
// performed, so "behind" only reflects the last time the repo was fetched.
public static class GitStatus
{
    private static readonly Dictionary<string, (DateTime at, string state)> _cache = new();
    private static readonly object _lock = new();

    public static string StateFor(string cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "";
        lock (_lock)
        {
            if (_cache.TryGetValue(cwd, out var c) && (DateTime.UtcNow - c.at).TotalSeconds < 45) return c.state;
            var s = Compute(cwd);
            _cache[cwd] = (DateTime.UtcNow, s);
            return s;
        }
    }

    private static string Compute(string cwd)
    {
        if (!Directory.Exists(cwd)) return "";
        try
        {
            if (Run(cwd, "rev-parse --is-inside-work-tree").Trim() != "true") return "";
            if (Run(cwd, "status --porcelain -- .").Trim().Length > 0) return "wip";

            foreach (var baseRef in new[] { "origin/main", "origin/master" })
            {
                var outp = Run(cwd, $"rev-list --left-right --count {baseRef}...HEAD").Trim();
                var parts = outp.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
                {
                    if (ahead > 0) return $"unpushed:{ahead}";
                    if (behind > 0) return $"behind:{behind}";
                    return "synced";
                }
            }
            return "synced";
        }
        catch { return ""; }
    }

    private static string Run(string cwd, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            var o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return o;
        }
        catch { return ""; }
    }
}
