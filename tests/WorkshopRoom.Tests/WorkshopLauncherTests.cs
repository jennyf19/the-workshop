namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="WorkshopLauncher.ParseRepo"/> — turning what the operator
/// types in the "use existing" field (owner/name, a github URL, or owner/name.git)
/// into the (owner, name) pair used to clone.
/// </summary>
public class WorkshopLauncherTests
{
    [Theory]
    [InlineData("jennyf19/the-workshop", "jennyf19", "the-workshop")]
    [InlineData("https://github.com/jennyf19/the-workshop", "jennyf19", "the-workshop")]
    [InlineData("https://github.com/jennyf19/the-workshop.git", "jennyf19", "the-workshop")]
    [InlineData("  jeferrie_microsoft/Ember_workshop  ", "jeferrie_microsoft", "Ember_workshop")]
    public void Parses_owner_and_name(string input, string owner, string name)
    {
        var (o, n) = WorkshopLauncher.ParseRepo(input);
        o.Should().Be(owner);
        n.Should().Be(name);
    }

    [Theory]
    [InlineData("just-a-name")]   // no owner
    [InlineData("")]
    [InlineData("a/b/c")]         // too many segments
    [InlineData("/")]
    public void Rejects_input_without_a_clear_owner_and_name(string input)
    {
        var (o, n) = WorkshopLauncher.ParseRepo(input);
        o.Should().BeNull();
        n.Should().BeNull();
    }
}
