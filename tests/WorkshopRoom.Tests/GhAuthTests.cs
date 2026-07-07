namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="GhAuth.Parse"/> — turning <c>gh auth status</c> text into
/// the signed-in logins with the active one marked. This feeds the owner picker
/// on the "new workshop" form (the PRInbox model: delegate to gh, store no token).
/// </summary>
public class GhAuthTests
{
    const string MultiAccount = @"
github.com
  ✓ Logged in to github.com account jennyf19 (keyring)
  - Active account: false
  - Git operations protocol: https
  - Token scopes: 'gist', 'read:org', 'repo', 'workflow'
  ✓ Logged in to github.com account jeferrie_microsoft (keyring)
  - Active account: true
  - Git operations protocol: https
  - Token scopes: 'repo', 'workflow'
";

    [Fact]
    public void Parses_multiple_logins_and_marks_the_active_one()
    {
        var logins = GhAuth.Parse(MultiAccount);

        logins.Select(l => l.Login).Should().Equal("jennyf19", "jeferrie_microsoft");
        logins.Single(l => l.IsActive).Login.Should().Be("jeferrie_microsoft");
    }

    [Fact]
    public void Single_account_without_an_active_line_defaults_to_active()
    {
        var text = "✓ Logged in to github.com account solo (keyring)\n  - Token scopes: 'repo'";

        var logins = GhAuth.Parse(text);

        logins.Should().ContainSingle();
        logins[0].Login.Should().Be("solo");
        logins[0].IsActive.Should().BeTrue();   // sole login defaults to active
    }

    [Fact]
    public void No_logins_yields_an_empty_list()
    {
        GhAuth.Parse("").Should().BeEmpty();
        GhAuth.Parse("You are not logged into any GitHub hosts.").Should().BeEmpty();
    }
}
