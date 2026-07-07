using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkshopRoom.Data;

public record WorkshopResult(bool Ok, string Message, string? Dir);

// Creates a new workshop: a private GitHub repo owned by the signed-in gh
// account the operator picked, cloned locally and scaffolded with the bench
// shape. Auth is delegated to gh (no stored tokens); to create under a specific
// owner we briefly make it the active gh account, then always restore.
public static class WorkshopLauncher
{
    public static WorkshopResult NewWorkshop(string owner, string name, string baseDir, string? activeAccount)
    {
        owner = (owner ?? "").Trim();
        name = (name ?? "").Trim();
        if (owner.Length == 0) return new(false, "pick an owner account", null);
        if (name.Length == 0) return new(false, "name the workshop", null);
        if (name.Any(char.IsWhiteSpace)) return new(false, "the name can't contain spaces", null);
        if (string.IsNullOrWhiteSpace(baseDir)) return new(false, "no base directory configured", null);

        var dir = Path.Combine(baseDir, name);
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
            return new(false, $"{dir} already exists and isn't empty", null);

        var switched = !string.Equals(owner, activeAccount, StringComparison.OrdinalIgnoreCase);
        try
        {
            if (switched)
            {
                var (okSwitch, switchOut) = GhAuth.Run($"auth switch -u {owner}");
                if (!okSwitch) return new(false, $"couldn't switch gh to {owner}: {Tail(switchOut)}", null);
            }

            var (okCreate, createOut) = GhAuth.Run($"repo create {owner}/{name} --private");
            if (!okCreate) return new(false, $"gh repo create failed: {Tail(createOut)}", null);

            try { Scaffold(dir, name); }
            catch (Exception ex) { return new(false, $"scaffold failed: {ex.Message}", dir); }

            var (okGit, gitOut) = InitAndPush(dir, owner, name);
            if (!okGit) return new(false, $"git init/push failed: {Tail(gitOut)}", dir);

            return new(true, $"created {owner}/{name} (private)", dir);
        }
        finally
        {
            // Always restore the previously-active account, even on failure.
            if (switched && !string.IsNullOrWhiteSpace(activeAccount))
                GhAuth.Run($"auth switch -u {activeAccount}");
        }
    }

    // Clones an existing private repo the operator already has into the base dir
    // and returns its folder, so the caller can open a desk in it. Acts as the
    // repo's owner if that's one of the signed-in accounts (for private access),
    // then always restores the active account.
    public static WorkshopResult UseWorkshop(string repo, string baseDir, string? activeAccount, IEnumerable<string> knownLogins)
    {
        repo = (repo ?? "").Trim();
        if (repo.Length == 0) return new(false, "enter a repo (owner/name)", null);
        if (string.IsNullOrWhiteSpace(baseDir)) return new(false, "no base directory configured", null);

        var (owner, name) = ParseRepo(repo);
        if (owner is null || name is null) return new(false, "use owner/name (or a github url)", null);

        var dir = Path.Combine(baseDir, name);
        if (Directory.Exists(Path.Combine(dir, ".git")))
            return new(true, $"using {owner}/{name} (already local)", dir);
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
            return new(false, $"{dir} exists but isn't a git clone", null);

        var actAs = knownLogins.FirstOrDefault(l => string.Equals(l, owner, StringComparison.OrdinalIgnoreCase));
        var switched = actAs is not null && !string.Equals(actAs, activeAccount, StringComparison.OrdinalIgnoreCase);
        try
        {
            if (switched)
            {
                var (okSwitch, switchOut) = GhAuth.Run($"auth switch -u {actAs}");
                if (!okSwitch) return new(false, $"couldn't switch gh to {actAs}: {Tail(switchOut)}", null);
            }
            var (okClone, cloneOut) = GhAuth.Run($"repo clone {owner}/{name} \"{dir}\"");
            if (!okClone) return new(false, $"gh repo clone failed: {Tail(cloneOut)}", null);
            return new(true, $"cloned {owner}/{name}", dir);
        }
        finally
        {
            if (switched && !string.IsNullOrWhiteSpace(activeAccount))
                GhAuth.Run($"auth switch -u {activeAccount}");
        }
    }

    // Parse "owner/name", a github URL, or "owner/name.git" into (owner, name).
    internal static (string? owner, string? name) ParseRepo(string input)
    {
        input = (input ?? "").Trim();
        input = Regex.Replace(input, @"^https?://github\.com/", "", RegexOptions.IgnoreCase);
        input = Regex.Replace(input, @"\.git$", "", RegexOptions.IgnoreCase);
        input = input.Trim('/');
        var parts = input.Split('/');
        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            return (parts[0], parts[1]);
        return (null, null);
    }

    public record WorkshopInfo(string Name, string Dir);

    // A folder is a workshop if it's a git repo that also carries the workshop
    // marker (hands-up.md at its root). The marker is what the scaffold writes,
    // so it cleanly separates a workshop from a plain clone — the product repo
    // and unrelated repos under the same base dir won't match.
    internal static bool IsWorkshop(string dir) =>
        Directory.Exists(Path.Combine(dir, ".git")) &&
        File.Exists(Path.Combine(dir, "hands-up.md"));

    // Lists the local workshops directly under baseDir so the room can offer
    // them as the place to open a desk. Sorted by name; unreadable folders are
    // skipped rather than throwing.
    public static List<WorkshopInfo> ListWorkshops(string baseDir)
    {
        var found = new List<WorkshopInfo>();
        if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) return found;
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(baseDir); }
        catch { return found; }
        foreach (var d in dirs)
        {
            try
            {
                if (IsWorkshop(d))
                    found.Add(new WorkshopInfo(Path.GetFileName(d.TrimEnd('\\', '/')), d));
            }
            catch { /* skip an unreadable folder */ }
        }
        return found.OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Scaffold(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "bench"));
        Directory.CreateDirectory(Path.Combine(dir, "desks"));
        File.WriteAllText(Path.Combine(dir, "README.md"), Readme(name));
        File.WriteAllText(Path.Combine(dir, "CAIRN.md"), Cairn);
        File.WriteAllText(Path.Combine(dir, "hands-up.md"), HandsUp);
        File.WriteAllText(Path.Combine(dir, "protocol.md"), Protocol);
        File.WriteAllText(Path.Combine(dir, "bench", ".gitkeep"), "");
        File.WriteAllText(Path.Combine(dir, "desks", ".gitkeep"), "");
    }

    private static (bool ok, string output) InitAndPush(string dir, string owner, string name)
    {
        var email = $"{owner}@users.noreply.github.com";
        var steps = new (string exe, string[] args)[]
        {
            ("git", new[] { "init", "-b", "main" }),
            ("git", new[] { "add", "-A" }),
            ("git", new[] { "-c", $"user.name={owner}", "-c", $"user.email={email}", "commit", "-m", "the workshop: scaffold the bench" }),
            ("git", new[] { "remote", "add", "origin", $"https://github.com/{owner}/{name}.git" }),
            ("git", new[] { "push", "-u", "origin", "main" }),
        };
        var log = "";
        foreach (var (exe, args) in steps)
        {
            var (ok, o) = Proc(exe, dir, args);
            log += $"$ {exe} {string.Join(' ', args)}\n{o}\n";
            if (!ok) return (false, log);
        }
        return (true, log);
    }

    // Runs a process with an explicit argument list (no shell quoting).
    private static (bool ok, string output) Proc(string exe, string cwd, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return (false, "");
            var o = p.StandardOutput.ReadToEnd();
            var e = p.StandardError.ReadToEnd();
            p.WaitForExit(90000);
            return (p.ExitCode == 0, o + e);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string Tail(string s) => s.Length > 220 ? "..." + s[^220..].Trim() : s.Trim();

    private static string Readme(string name) =>
$@"# {name}

a workshop — a room of long-running AI agents (desks) sharing one bench.

- `bench/` — the shared, append-only artifact the desks work on
- `desks/` — one folder per desk (its journal and memory)
- `hands-up.md` — decisions the room surfaces to the operator
- `protocol.md` — how desks take turns and disagree
- `CAIRN.md` — the operating disposition each desk reads first
";

    private const string Cairn =
@"# CAIRN — how a desk stands

read this before you start. it's the disposition that wins under pressure.

- stop is a valid finish. if you can't verify it, say so; don't force a result.
- applied means it builds. a change isn't done until it's run and checked.
- never bluff. ""i don't know"" beats a confident guess.
- hold your scope. do the task asked; flag the rest, don't silently expand it.
- your verdict is yours. if you'd send it back, say SEND-BACK.
- leave the bench better marked for the next desk than you found it.
";

    private const string HandsUp =
@"# hands-up

decisions the room couldn't settle against the facts, surfaced to the operator.
one line each; the operator reads this, not the transcripts.

<!-- desks append below; the operator clears what's handled -->
";

    private const string Protocol =
@"# protocol

- the bench is append-only. add your turn; don't rewrite another desk's lines.
- disagree in the open. SEND-BACK is a valid output; agreement you didn't test isn't.
- settle against facts — code, tests, a fresh desk — not against each other.
- fetch + rebase before you push, so the room doesn't step on itself.
- when you can't settle it, raise a hand (hands-up.md); don't force it.
";
}
