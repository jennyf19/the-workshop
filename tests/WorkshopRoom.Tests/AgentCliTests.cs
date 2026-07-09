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
}
