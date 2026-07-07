using System.Diagnostics;

namespace WorkshopRoom.Data;

// Closes a desk that still has a live agent. The desk holds an inuse.<pid>.lock
// while its CLI is open, so we end that process, clear the stale lock, then run
// one headless pass that resumes the same session and asks the agent to write
// its end-of-desk journal. The journal pass is fire-and-forget: it writes on
// its own and exits, so the board stays responsive. Idle desks never reach here
// (the caller just hides them).
public static class DeskCloser
{
    private static string SessionDir(string sessionId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot", "session-state", sessionId);

    public static void Close(string sessionId, string cwd, int consolePid, string deskName)
    {
        if (consolePid > 0) TryKill(consolePid);
        ClearLock(sessionId);
        // Give the CLI a moment to fully release before we resume the session.
        Thread.Sleep(1200);
        StartJournalPass(sessionId, cwd, deskName);
    }

    private static void TryKill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(4000);
        }
        catch { /* already gone, or not ours to kill */ }
    }

    // Remove any lingering inuse.*.lock so `--resume` doesn't refuse the session.
    private static void ClearLock(string sessionId)
    {
        try
        {
            var dir = SessionDir(sessionId);
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "inuse.*.lock"))
                try { File.Delete(f); } catch { /* best effort */ }
        }
        catch { /* best effort */ }
    }

    // Resume the session non-interactively and ask the agent to journal, then
    // stop. --allow-all-tools is required in non-interactive mode so the write
    // doesn't block on a permission prompt.
    private static void StartJournalPass(string sessionId, string cwd, string deskName)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var name = string.IsNullOrWhiteSpace(deskName) ? "desk" : deskName.Trim();
        var prompt =
            "you're being wound down from the workshop board. write a short end-of-desk " +
            "journal entry (what you worked on, the current state, and the next step) and " +
            $"append it to ./desks/{name}/journal.md, creating the folders if needed. keep " +
            "it to a few lines, then stop.";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "copilot",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd)) psi.WorkingDirectory = cwd;
            psi.ArgumentList.Add($"--resume={sessionId}");
            psi.ArgumentList.Add("--allow-all-tools");
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(prompt);
            Process.Start(psi);   // fire-and-forget; it journals and exits on its own
        }
        catch { /* best effort — a failed journal shouldn't block the close */ }
    }
}
