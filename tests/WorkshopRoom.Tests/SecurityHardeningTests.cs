namespace WorkshopRoom.Tests;

/// <summary>
/// Pins the input-safety guards that keep operator- or file-supplied strings
/// from escaping their base directory (path traversal) or reaching a terminal /
/// gh command line (OS-command / argument injection). These are the fixes from
/// the security review: desk/workshop name sanitization, gh owner validation,
/// and the terminal working-directory guard.
/// </summary>
public class SecurityHardeningTests
{
    [Theory]
    [InlineData("reviewer", true)]
    [InlineData("acme-api", true)]
    [InlineData("my_desk", true)]
    [InlineData("desk.1", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("../evil", false)]
    [InlineData("..\\evil", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("C:\\abs", false)]
    [InlineData("a\"b", false)]
    [InlineData("a<b", false)]
    public void IsSafeName_allows_simple_segments_and_rejects_traversal(string name, bool expected)
        => WorkshopLauncher.IsSafeName(name).Should().Be(expected);

    [Theory]
    [InlineData("reviewer", "reviewer")]
    [InlineData("  reviewer  ", "reviewer")]
    [InlineData("..\\..\\evil", "desk")]
    [InlineData("a/b", "desk")]
    [InlineData("x\" ; calc ; \"", "desk")]
    [InlineData("", "desk")]
    [InlineData(null, "desk")]
    public void SafeDeskName_returns_a_safe_segment_or_desk(string? input, string expected)
        => WorkshopLauncher.SafeDeskName(input).Should().Be(expected);

    [Theory]
    [InlineData("jennyf19", true)]
    [InlineData("acme-org", true)]
    [InlineData("A1", true)]
    [InlineData("", false)]
    [InlineData("bad owner", false)]
    [InlineData("a;b", false)]
    [InlineData("-lead", false)]      // leading '-' would read as a gh flag
    [InlineData("a/b", false)]
    public void IsSafeAccount_matches_github_login_shape(string owner, bool expected)
        => WorkshopLauncher.IsSafeAccount(owner).Should().Be(expected);

    [Theory]
    [InlineData("../evil")]
    [InlineData("..\\..\\evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void NewMiniWorkshop_rejects_traversal_and_separators(string name)
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var r = WorkshopLauncher.NewMiniWorkshop(name, baseDir.FullName);
            r.Ok.Should().BeFalse();
            r.Dir.Should().BeNull();
        }
        finally { baseDir.Delete(recursive: true); }
    }

    [Fact]
    public void PrepareDesk_keeps_the_desk_inside_the_workshop_even_for_a_malicious_name()
    {
        var ws = Directory.CreateTempSubdirectory();
        try
        {
            var deskDir = WorkshopLauncher.PrepareDesk(ws.FullName, "..\\..\\evil");

            var expectedBase = Path.GetFullPath(Path.Combine(ws.FullName, "desks"));
            Path.GetFullPath(deskDir).Should().StartWith(expectedBase);

            // the traversal target was never created outside the workshop
            var escaped = Path.GetFullPath(Path.Combine(ws.FullName, "..", "..", "evil"));
            Directory.Exists(escaped).Should().BeFalse();
        }
        finally { ws.Delete(recursive: true); }
    }

    [Fact]
    public void SafeDir_accepts_a_real_directory_and_rejects_injected_or_missing_paths()
    {
        var real = Directory.CreateTempSubdirectory();
        try
        {
            ConsoleLauncher.SafeDir(real.FullName).Should().BeTrue();
            ConsoleLauncher.SafeDir("C:\\\" ; start calc").Should().BeFalse();   // quote-bearing pseudo-path
            ConsoleLauncher.SafeDir(Path.Combine(real.FullName, "nope-" + Guid.NewGuid())).Should().BeFalse();
            ConsoleLauncher.SafeDir("").Should().BeFalse();
            ConsoleLauncher.SafeDir(null).Should().BeFalse();
            ConsoleLauncher.SafeDir(real.FullName + "\"").Should().BeFalse();     // real path + a quote -> unsafe
        }
        finally { real.Delete(recursive: true); }
    }

    [Fact]
    public void NewWorkshop_rejects_an_invalid_owner_before_touching_gh()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var r = WorkshopLauncher.NewWorkshop("bad owner!", "proj", baseDir.FullName, "someone");
            r.Ok.Should().BeFalse();
            r.Message.Should().Contain("owner");
            Directory.Exists(Path.Combine(baseDir.FullName, "proj")).Should().BeFalse();
        }
        finally { baseDir.Delete(recursive: true); }
    }

    [Fact]
    public void NewWorkshop_rejects_a_traversal_name_before_touching_gh()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var r = WorkshopLauncher.NewWorkshop("jennyf19", "..\\evil", baseDir.FullName, "jennyf19");
            r.Ok.Should().BeFalse();
            r.Message.Should().Contain("simple repo name");
        }
        finally { baseDir.Delete(recursive: true); }
    }
}
