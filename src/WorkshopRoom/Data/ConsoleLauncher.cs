using System.Diagnostics;

namespace WorkshopRoom.Data;

// Opens a desk's live Copilot CLI session in a new terminal — the room's
// "console-to-session" link. Resumes by the session id
// (which is the session-state folder name) in the desk's working directory.
public static class ConsoleLauncher
{
    public static bool Open(string sessionId, string cwd, string agentKey = "copilot", AgentLaunchSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;
        // Reopen the desk with the shape the launching agent needs. An Agency
        // desk comes back through its wrapper (MCPs/plugin re-armed) instead of
        // as a bare Copilot process that silently drops its tools (issue #2).
        var resume = AgentClis.BuildResume(AgentClis.ByKey(agentKey), sessionId, settings);

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
    // agent-CLI session there in a new terminal. agentKey is resolved against
    // the AgentClis whitelist, and every option the room fills in is validated,
    // so an arbitrary command can never reach the shell. Copilot gets --name +
    // an orientation prompt (-i); Agency is launched wrapped with its MCPs,
    // plugin, model and agent (issue #2) instead of bare.
    public static bool NewDesk(string dir, string name, string? orient = null, string agentKey = "copilot", AgentLaunchSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try { Directory.CreateDirectory(dir); } catch { return false; }

        var agent = AgentClis.ByKey(agentKey);
        var invoke = AgentClis.BuildLaunch(agent, name, orient, settings);

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
