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
    // agent-CLI session there in a new terminal. Copilot supports --name and an
    // orientation prompt (-i) so the desk auto-runs it; other agents (agency,
    // claude) are launched bare in the folder. agentKey is resolved against the
    // AgentClis whitelist, so an arbitrary command can never reach the shell.
    public static bool NewDesk(string dir, string name, string? orient = null, string agentKey = "copilot")
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try { Directory.CreateDirectory(dir); } catch { return false; }

        var agent = AgentClis.ByKey(agentKey);
        string invoke;
        if (string.Equals(agent.Key, AgentClis.Copilot.Key, StringComparison.OrdinalIgnoreCase))
        {
            var nameArg = string.IsNullOrWhiteSpace(name) ? "" : $" --name \"{name}\"";
            var orientArg = string.IsNullOrWhiteSpace(orient) ? "" : $" -i \"{orient}\"";
            invoke = $"{agent.Command}{nameArg}{orientArg}";
        }
        else
        {
            invoke = agent.Command;   // whitelisted command, launched in the desk folder
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = $"-d \"{dir}\" {invoke}", UseShellExecute = true });
            return true;
        }
        catch { /* fall through */ }

        try
        {
            var psi = new ProcessStartInfo { FileName = "pwsh.exe", Arguments = $"-NoExit -Command \"{invoke}\"", UseShellExecute = true, WorkingDirectory = dir };
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }
}
