namespace WorkshopRoom.Data;

// An agent CLI a desk can be opened with. The workshop stays CLI-native: each
// agent is just a command launched in the desk's folder. Copilot (GHCP CLI) is
// the default and always offered; others are surfaced only when installed, so
// the-workshop itself carries no dependency on them.
//
// Sub is the engine subcommand a wrapper drives — Agency drives Copilot, so its
// invocation is "agency copilot ...". SupportsName is whether the agent takes
// Copilot's --name (Agency can't: it's mutually exclusive with the --resume
// Agency uses internally). Configurable is whether the agent accepts the
// --mcp/--plugin/--model/--agent options the room fills in.
public record AgentCli(
    string Key, string Command, string Label,
    string? Sub = null, bool SupportsName = true, bool Configurable = false);

// How the room fills in a configurable agent's invocation — the pr-inbox shape
// (--mcp workiq --mcp teams --plugin ... --model ... --agent ...). Values are
// resolved from appsettings + environment overrides at startup. ResumeMode
// selects how the desk is reopened (see AgentClis.BuildResume).
public sealed record AgentLaunchSettings(
    string Mcps = "", string Plugin = "", string Model = "", string Agent = "",
    string ResumeMode = "wrapper");

public static class AgentClis
{
    public static readonly AgentCli Copilot = new("copilot", "copilot", "GHCP CLI");

    // The agents the room knows how to launch. Copilot is always available;
    // agency and claude appear only when their command resolves on PATH. Agency
    // wraps Copilot (Sub), can't take --name, and is configurable with MCPs etc.
    private static readonly AgentCli[] Known =
    {
        Copilot,
        new("agency", "agency", "Agency", Sub: "copilot", SupportsName: false, Configurable: true),
        new("claude", "claude", "Claude Code", SupportsName: false),
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

    // --- invocation building -------------------------------------------------

    // Characters allowed in a config token (an MCP name, plugin ref, model, or
    // agent id). Covers real values — "github:1ES-microsoft/ai-plugins:plugins/x",
    // "claude-opus-4.7-xhigh", "security-toolkit:dual-model-review". Anything
    // outside this set (quotes, spaces, ; & | $ ` < > ( ) …) is dropped rather
    // than passed on — the same "never reaches a shell" guarantee ByKey gives
    // the executable, extended to the flags the room fills in.
    internal static bool SafeToken(string? s) =>
        !string.IsNullOrEmpty(s) && s.All(c => char.IsLetterOrDigit(c) || "._:/@-".Contains(c));

    // The --mcp/--plugin/--model/--agent flags for a configurable agent, each
    // value validated. Returns "" (no leading space) when the agent isn't
    // configurable or nothing is set. Unsafe values are silently skipped.
    internal static string ConfigFlags(AgentCli agent, AgentLaunchSettings? s)
    {
        if (!agent.Configurable || s is null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var mcp in (s.Mcps ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (SafeToken(mcp)) sb.Append(" --mcp ").Append(mcp);
        if (SafeToken(s.Plugin)) sb.Append(" --plugin ").Append(s.Plugin);
        if (SafeToken(s.Model)) sb.Append(" --model ").Append(s.Model);
        if (SafeToken(s.Agent)) sb.Append(" --agent ").Append(s.Agent);
        return sb.ToString();
    }

    // The command line for a brand-new desk. Copilot:
    //     copilot --name "X" -i "orient"
    // Agency (configurable, no --name — it clashes with Agency's own --resume):
    //     agency copilot --mcp workiq --mcp teams --plugin P --model M --agent A -i "orient"
    // This is the fix for the "launched bare" half of issue #2: an Agency desk
    // now loads its MCPs/plugin instead of coming up with none.
    public static string BuildLaunch(AgentCli agent, string? name, string? orient, AgentLaunchSettings? settings)
    {
        var head = agent.Sub is null ? agent.Command : $"{agent.Command} {agent.Sub}";
        var cfg = ConfigFlags(agent, settings);
        var nameArg = agent.SupportsName && !string.IsNullOrWhiteSpace(name) ? $" --name \"{name}\"" : "";
        var orientArg = string.IsNullOrWhiteSpace(orient) ? "" : $" -i \"{orient}\"";
        return $"{head}{cfg}{nameArg}{orientArg}";
    }

    // The command line to reopen an existing session. Copilot keeps the bare
    // shape. Agency reproduces its wrapper so the desk comes back with its
    // MCPs/plugin instead of as a plain Copilot process — the "resume disarmed"
    // half of issue #2:
    //     wrapper (default): agency copilot <config flags> --resume <id>
    //     gateway:           copilot --session-manager --mcp gateway --resume <id>
    //
    // OPEN QUESTION (JM, issue #2): are the explicit --mcp flags interchangeable
    // with Agency's internal "--mcp gateway" on resume — i.e. does a resumed
    // Agency session re-derive its MCP set from the session record, or must the
    // flags be re-passed?
    //
    // Checked against PRInbox (JM's own tool, origin/main 2026-07-01): it never
    // programmatically resumes — every review is a fresh `agency copilot …`
    // launch with a fresh --name, and any real resume is left to Copilot's
    // interactive `--resume` picker. So neither shape below is battle-tested.
    // Note the gateway shape is Agency's *own* relaunch form (copilot.rs
    // handoff_relaunch_args), so it may in fact be the more correct default for
    // reopening an existing Agency session; the wrapper shape bolts an external
    // --resume onto the fresh-launch form and could fight Agency's internal
    // --resume <uuid>. Until that's confirmed the room defaults to the wrapper
    // (self-contained, re-passes config) and exposes the gateway shape via
    // WORKSHOP_AGENCY_RESUME=gateway, so the maintainer can flip and test it
    // without a code change.
    public static string BuildResume(AgentCli agent, string sessionId, AgentLaunchSettings? settings)
    {
        var id = SafeToken(sessionId) ? sessionId : "";
        if (agent.Sub is not null)   // a wrapped engine, e.g. Agency
        {
            if (string.Equals(settings?.ResumeMode, "gateway", StringComparison.OrdinalIgnoreCase))
                return $"{agent.Sub} --session-manager --mcp gateway --resume {id}";
            var cfg = ConfigFlags(agent, settings);
            return $"{agent.Command} {agent.Sub}{cfg} --resume {id}";
        }
        return $"copilot --resume={id}";
    }
}
