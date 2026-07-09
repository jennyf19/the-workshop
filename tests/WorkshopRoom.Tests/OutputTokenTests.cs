namespace WorkshopRoom.Tests;

/// <summary>
/// Tokens are summed straight from the local event stream: every
/// assistant.message carries data.outputTokens, so a desk's output-token count
/// is derived on the fly with no cloud usage sync. These tests pin that the sum
/// is correct and that a desk with no token events reports zero (not a crash).
/// </summary>
public class OutputTokenTests
{
    private static SessionStoreReader MakeReader(string root, string tmp) => new(
        root: root,
        usageCache: Path.Combine(tmp, "usage.json"),
        namesPath: Path.Combine(tmp, "names.json"),
        resolvedPath: Path.Combine(tmp, "handsup-resolved.json"),
        closedPath: Path.Combine(tmp, "closed-desks.json"));

    private static void WriteSession(string root, string sessionId, string name, params long[] outputTokens)
    {
        var sdir = Path.Combine(root, sessionId);
        Directory.CreateDirectory(sdir);
        File.WriteAllText(Path.Combine(sdir, "workspace.yaml"),
            $"name: {name}\nuser_named: true\ncwd: {sdir}\n");

        var lines = new List<string>
        {
            "{\"type\":\"session.start\",\"data\":{}}",
            "{\"type\":\"user.message\",\"data\":{\"content\":\"go\"}}",
        };
        foreach (var t in outputTokens)
            lines.Add($"{{\"type\":\"assistant.message\",\"data\":{{\"content\":\"ok\",\"model\":\"claude-test\",\"outputTokens\":{t}}}}}");
        File.WriteAllText(Path.Combine(sdir, "events.jsonl"), string.Join("\n", lines) + "\n");
    }

    [Fact]
    public void Desk_output_tokens_sum_the_assistant_message_events()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var root = Path.Combine(tmp.FullName, "session-state");
            Directory.CreateDirectory(root);
            WriteSession(root, "aaaaaaaa-token-sum", "adder", 100, 250, 75);

            var snap = MakeReader(root, tmp.FullName).GetSnapshot();

            var desk = snap.Desks.Should().ContainSingle().Subject;
            desk.TokensOut.Should().Be(425);   // 100 + 250 + 75
            desk.TokensIn.Should().Be(0);       // input isn't tracked locally per-turn
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void Desk_with_no_token_events_reports_zero()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var root = Path.Combine(tmp.FullName, "session-state");
            Directory.CreateDirectory(root);
            WriteSession(root, "bbbbbbbb-no-tokens", "quiet");   // no outputTokens events

            var snap = MakeReader(root, tmp.FullName).GetSnapshot();

            var desk = snap.Desks.Should().ContainSingle().Subject;
            desk.TokensOut.Should().Be(0);
        }
        finally { tmp.Delete(recursive: true); }
    }

    [Fact]
    public void Desk_model_is_the_latest_assistant_message_model()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var root = Path.Combine(tmp.FullName, "session-state");
            var sdir = Path.Combine(root, "cccccccc-model-switch");
            Directory.CreateDirectory(sdir);
            File.WriteAllText(Path.Combine(sdir, "workspace.yaml"), $"name: switcher\nuser_named: true\ncwd: {sdir}\n");
            File.WriteAllLines(Path.Combine(sdir, "events.jsonl"), new[]
            {
                "{\"type\":\"user.message\",\"data\":{\"content\":\"go\"}}",
                "{\"type\":\"assistant.message\",\"data\":{\"content\":\"a\",\"model\":\"claude-opus-4.6\",\"outputTokens\":10}}",
                "{\"type\":\"session.model_change\",\"data\":{}}",
                "{\"type\":\"assistant.message\",\"data\":{\"content\":\"b\",\"model\":\"gpt-5.3-codex\",\"outputTokens\":20}}",
            });

            var snap = MakeReader(root, tmp.FullName).GetSnapshot();

            var desk = snap.Desks.Should().ContainSingle().Subject;
            desk.Model.Should().Be("gpt-5.3-codex");   // latest wins
        }
        finally { tmp.Delete(recursive: true); }
    }
}
