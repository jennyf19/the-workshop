namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for <see cref="SessionStoreReader.ResolveName"/> and
/// <see cref="SessionStoreReader.MeaningfulLeaf"/> — how a desk earns a human
/// name. Precedence: an explicit id override, then a cwd override, then the
/// CLI's own user-given name, then the meaningful last segment of the working
/// directory, then the raw name, and finally a "session &lt;id&gt;" fallback.
/// This is why a desk reads "The_Workshop" or "classroom" instead of the
/// auto-generated "Create New Project".
/// </summary>
public class NameResolutionTests
{
    static Dictionary<string, string> Map(params (string key, string val)[] pairs)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    static readonly Dictionary<string, string> None = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Id_override_wins_over_everything()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", @"D:\repo\service", userNamed: true, rawName: "Raw Name",
            byId: Map(("abc123", "Pinned")), byCwd: Map((@"D:\repo\service", "ByCwd")));

        name.Should().Be("Pinned");
    }

    [Fact]
    public void Cwd_override_wins_over_user_name_and_folder_leaf()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", @"D:\repo\service", userNamed: true, rawName: "Raw Name",
            byId: None, byCwd: Map((@"D:\repo\service", "ByCwd")));

        name.Should().Be("ByCwd");
    }

    [Fact]
    public void User_given_name_wins_over_the_folder_leaf()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", @"D:\repo\service", userNamed: true, rawName: "The_Workshop",
            byId: None, byCwd: None);

        name.Should().Be("The_Workshop");
    }

    [Fact]
    public void Falls_back_to_the_folder_leaf_when_not_user_named()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", @"D:\projects\billing-api", userNamed: false, rawName: "Untitled Session",
            byId: None, byCwd: None);

        name.Should().Be("billing-api");
    }

    [Fact]
    public void Falls_back_to_raw_name_when_cwd_has_no_meaningful_leaf()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", @"D:\", userNamed: false, rawName: "Raw Name",
            byId: None, byCwd: None);

        name.Should().Be("Raw Name");
    }

    [Fact]
    public void Falls_back_to_the_session_id_when_nothing_else_is_known()
    {
        var name = SessionStoreReader.ResolveName(
            "abc123", cwd: "", userNamed: false, rawName: "",
            byId: None, byCwd: None);

        name.Should().Be("session abc123");
    }

    [Theory]
    [InlineData(@"D:\projects\billing-api", "billing-api")]
    [InlineData(@"D:\projects\billing-api\", "billing-api")]   // trailing slash trimmed
    [InlineData("/usr/local/bin", "bin")]
    [InlineData("C:/a/b", "b")]
    public void Meaningful_leaf_is_the_last_path_segment(string cwd, string expected)
        => SessionStoreReader.MeaningfulLeaf(cwd).Should().Be(expected);

    [Theory]
    [InlineData(@"D:\")]   // bare drive root
    [InlineData("D:")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Meaningful_leaf_is_null_for_roots_and_blanks(string? cwd)
        => SessionStoreReader.MeaningfulLeaf(cwd).Should().BeNull();
}
