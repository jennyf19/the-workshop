using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkshopRoom.Data;

// One GitHub login the gh CLI is signed into on this machine.
public record GhLogin(string Login, bool IsActive);

// Discovers the signed-in GitHub logins by delegating to the gh CLI — the
// PRInbox model: never store a token, just ask gh who's signed in and let the
// operator pick which account owns a new workshop.
public static class GhAuth
{
    public static IReadOnlyList<GhLogin> DiscoverLogins(string host = "github.com")
    {
        var (ok, output) = Run($"auth status --hostname {host}");
        return ok ? Parse(output) : Array.Empty<GhLogin>();
    }

    // Parse `gh auth status` text into the logins it reports, marking the active
    // one. Kept pure for testing. Handles the multi-account format:
    //   ✓ Logged in to github.com account <login> (keyring)
    //     - Active account: true
    internal static IReadOnlyList<GhLogin> Parse(string ghAuthStatus)
    {
        var logins = new List<GhLogin>();
        foreach (var raw in (ghAuthStatus ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            var m = Regex.Match(line, @"account\s+(\S+)");
            if (line.Contains("Logged in", StringComparison.OrdinalIgnoreCase) && m.Success)
            {
                logins.Add(new GhLogin(m.Groups[1].Value, false));
                continue;
            }
            if (logins.Count > 0 && Regex.IsMatch(line, @"Active account:\s*true", RegexOptions.IgnoreCase))
                logins[^1] = logins[^1] with { IsActive = true };
        }
        // Older single-account gh omits the "Active account" line — default the
        // first login to active so the picker always has a default.
        if (logins.Count > 0 && !logins.Any(l => l.IsActive))
            logins[0] = logins[0] with { IsActive = true };
        return logins;
    }

    // Runs a gh command (arguments are simple, no quoted values). Returns
    // (success, combined stdout+stderr). Never throws.
    internal static (bool ok, string output) Run(string args, string? cwd = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrWhiteSpace(cwd)) psi.WorkingDirectory = cwd;
            using var p = Process.Start(psi);
            if (p is null) return (false, "");
            var o = p.StandardOutput.ReadToEnd();
            var e = p.StandardError.ReadToEnd();
            p.WaitForExit(20000);
            return (p.ExitCode == 0, o + "\n" + e);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
