namespace WorkshopRoom.Tests;

/// <summary>
/// The agent-CLI registry decides which agents a desk can be opened with.
/// Copilot is always available and is the safe default; ByKey is the whitelist
/// that keeps an arbitrary key from ever resolving to an unknown command.
/// </summary>
public class AgentCliTests
{
    [Fact]
    public void Available_always_lists_copilot_first()
    {
        var available = AgentClis.Available();

        available.Should().NotBeEmpty();
        available[0].Key.Should().Be("copilot");
        available.Should().Contain(a => a.Key == "copilot");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("rm -rf /")]
    public void ByKey_defaults_to_copilot_for_unknown_or_null(string? key)
    {
        AgentClis.ByKey(key).Should().Be(AgentClis.Copilot);
    }

    [Theory]
    [InlineData("agency", "agency")]
    [InlineData("claude", "claude")]
    [InlineData("COPILOT", "copilot")]   // case-insensitive
    public void ByKey_resolves_known_agents(string key, string expectedKey)
    {
        AgentClis.ByKey(key).Key.Should().Be(expectedKey);
    }

    [Fact]
    public void ByKey_command_is_never_arbitrary_input()
    {
        // Even a shell-injection-shaped key resolves to the safe copilot command.
        AgentClis.ByKey("agency; del *").Command.Should().Be("copilot");
    }

    // --- launch / resume invocation building (issue #2) ---
    // Agency is launched and resumed *wrapped* (with its MCPs/plugin) instead of
    // bare; Copilot keeps its existing shape. Every value the room fills in is
    // validated, so a config string can't smuggle a command onto the shell.

    static readonly AgentCli Agency = AgentClis.ByKey("agency");

    [Fact]
    public void Copilot_launch_keeps_the_name_and_orientation_prompt()
        => AgentClis.BuildLaunch(AgentClis.Copilot, "reviewer", "read brief.md", settings: null)
            .Should().Be("copilot --name \"reviewer\" -i \"read brief.md\"");

    [Fact]
    public void Agency_launch_wraps_copilot_with_its_mcps_and_no_name()
    {
        var cmd = AgentClis.BuildLaunch(Agency, "reviewer", "read brief.md",
            new AgentLaunchSettings(Mcps: "workiq,teams"));

        cmd.Should().Be("agency copilot --mcp workiq --mcp teams -i \"read brief.md\"");
        cmd.Should().NotContain("--name");   // --name clashes with Agency's own --resume
    }

    [Fact]
    public void Agency_launch_includes_plugin_model_and_agent_when_set()
        => AgentClis.BuildLaunch(Agency, "r", null, new AgentLaunchSettings(
                Mcps: "workiq", Plugin: "github:org/repo:plugins/x",
                Model: "claude-opus-4.7-xhigh", Agent: "security-toolkit:dual-model-review"))
            .Should().Be("agency copilot --mcp workiq --plugin github:org/repo:plugins/x --model claude-opus-4.7-xhigh --agent security-toolkit:dual-model-review");

    [Fact]
    public void Copilot_resume_is_the_bare_shape()
        => AgentClis.BuildResume(AgentClis.Copilot, "abc-123", settings: null)
            .Should().Be("copilot --resume=abc-123");

    [Fact]
    public void Agency_resume_reproduces_the_wrapper_by_default()
        => AgentClis.BuildResume(Agency, "abc-123", new AgentLaunchSettings(Mcps: "workiq,teams"))
            .Should().Be("agency copilot --mcp workiq --mcp teams --resume abc-123");

    [Fact]
    public void Agency_resume_can_use_the_gateway_shape()
        => AgentClis.BuildResume(Agency, "abc-123", new AgentLaunchSettings(Mcps: "workiq,teams", ResumeMode: "gateway"))
            .Should().Be("copilot --session-manager --mcp gateway --resume abc-123");

    [Theory]
    [InlineData("workiq", true)]
    [InlineData("github:1ES-microsoft/ai-plugins:plugins/security-toolkit", true)]
    [InlineData("claude-opus-4.7-xhigh", true)]
    [InlineData("bad token", false)]   // space
    [InlineData("bad;token", false)]   // command separator
    [InlineData("a|b", false)]         // pipe
    [InlineData("$(x)", false)]        // subshell
    [InlineData("", false)]
    public void SafeToken_allows_only_flag_safe_values(string token, bool ok)
        => AgentClis.SafeToken(token).Should().Be(ok);

    [Fact]
    public void Unsafe_mcp_entries_are_dropped_from_the_invocation()
        => AgentClis.BuildLaunch(Agency, "r", null, new AgentLaunchSettings(Mcps: "workiq,bad;token,teams"))
            .Should().Be("agency copilot --mcp workiq --mcp teams");   // bad;token dropped

    [Fact]
    public void An_injection_shaped_plugin_never_reaches_the_command()
    {
        var cmd = AgentClis.BuildLaunch(Agency, "r", null, new AgentLaunchSettings(Plugin: "x; del *"));
        cmd.Should().NotContain("del");
        cmd.Should().Be("agency copilot");   // nothing configured survived validation
    }
}
