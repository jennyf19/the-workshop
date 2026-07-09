namespace WorkshopRoom.Data;

// An agent CLI a desk can be opened with. The workshop stays CLI-native: each
// agent is just a command launched in the desk's folder. Copilot (GHCP CLI) is
// the default and always offered; others are surfaced only when installed, so
// the-workshop itself carries no dependency on them.
public record AgentCli(string Key, string Command, string Label);

public static class AgentClis
{
    public static readonly AgentCli Copilot = new("copilot", "copilot", "GHCP CLI");

    // The agents the room knows how to launch. Copilot is always available;
    // agency and claude appear only when their command resolves on PATH.
    private static readonly AgentCli[] Known =
    {
        Copilot,
        new("agency", "agency", "Agency"),
        new("claude", "claude", "Claude Code"),
    };

    // Copilot first (always), then any other known agent whose command is
    // actually installed on this machine.
    public static List<AgentCli> Available()
    {
        var list = new List<AgentCli> { Copilot };
        foreach (var a in Known)
            if (!string.Equals(a.Key, Copilot.Key, StringComparison.OrdinalIgnoreCase) && IsOnPath(a.Command))
                list.Add(a);
        return list;
    }

    // Resolves a key to a known agent, defaulting to Copilot. This is the
    // whitelist that keeps an arbitrary command from ever reaching a shell.
    public static AgentCli ByKey(string? key) =>
        Known.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Copilot;

    // True if an executable named `command` is found on PATH (honoring PATHEXT
    // on Windows). Used to decide which agents to offer.
    internal static bool IsOnPath(string command)
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            var exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    if (File.Exists(Path.Combine(dir, command))) return true;
                    foreach (var ext in exts)
                        if (File.Exists(Path.Combine(dir, command + ext))) return true;
                }
                catch { /* an unreadable PATH entry — skip it */ }
            }
        }
        catch { /* PATH unavailable — treat as not found */ }
        return false;
    }
}
