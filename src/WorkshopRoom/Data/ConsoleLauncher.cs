using System.Diagnostics;

namespace WorkshopRoom.Data;

// Opens a desk's live Copilot CLI session in a new terminal — the room's
// "console-to-session" link. Resumes by the session id
// (which is the session-state folder name) in the desk's working directory.
public static class ConsoleLauncher
{
    public static bool Open(string sessionId, string cwd)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;
        var resume = $"copilot --resume={sessionId}";

        // Prefer Windows Terminal: opens a clean tab in the desk's folder.
        try
        {
            var args = string.IsNullOrWhiteSpace(cwd) ? resume : $"-d \"{cwd}\" {resume}";
            Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = args, UseShellExecute = true });
            return true;
        }
        catch { /* fall through */ }

        // Fallback: pwsh that stays open.
        try
        {
            var psi = new ProcessStartInfo { FileName = "pwsh.exe", Arguments = $"-NoExit -Command \"{resume}\"", UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(cwd)) psi.WorkingDirectory = cwd;
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }

    // Starts a brand-new desk: ensures the folder exists, then opens a fresh
    // Copilot CLI session there (named after the folder) in a new terminal.
    public static bool NewDesk(string dir, string name)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try { Directory.CreateDirectory(dir); } catch { return false; }
        var nameArg = string.IsNullOrWhiteSpace(name) ? "" : $" --name \"{name}\"";

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = $"-d \"{dir}\" copilot{nameArg}", UseShellExecute = true });
            return true;
        }
        catch { /* fall through */ }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "pwsh.exe", Arguments = $"-NoExit -Command \"copilot{nameArg}\"", UseShellExecute = true, WorkingDirectory = dir });
            return true;
        }
        catch { return false; }
    }
}
