namespace WorkshopRoom.Tests;

/// <summary>
/// Tests for the operator "dismiss" action on TA (coordination) alerts. Alerts
/// are derived from signal files on every refresh, so a dismissal is persisted
/// and keyed to (desk, summary, emittedAt): the same alert stays gone across
/// refreshes, but a newer signal (later EmittedAt) yields a different key and
/// re-raises it.
/// </summary>
public class AlertDismissTests
{
    static readonly DateTime T0 = new(2026, 7, 8, 13, 28, 9, DateTimeKind.Utc);

    [Fact]
    public void Alert_key_is_stable_and_scoped_by_desk_summary_and_time()
    {
        var key = SessionStoreReader.AlertKey("workshop-product", "Need a decision", T0);

        SessionStoreReader.AlertKey("workshop-product", "Need a decision", T0).Should().Be(key);        // stable
        SessionStoreReader.AlertKey("code-review", "Need a decision", T0).Should().NotBe(key);          // desk-scoped
        SessionStoreReader.AlertKey("workshop-product", "Different thing", T0).Should().NotBe(key);     // summary-scoped
        SessionStoreReader.AlertKey("workshop-product", "Need a decision", T0.AddMinutes(1)).Should().NotBe(key); // time-scoped
    }

    [Fact]
    public void Dismissing_persists_and_a_fresh_reader_sees_it()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var alertsPath = Path.Combine(dir.FullName, "alerts-dismissed.json");
            SessionStoreReader Make() => new(
                root: Path.Combine(dir.FullName, "no-sessions"),
                usageCache: Path.Combine(dir.FullName, "usage.json"),
                namesPath: Path.Combine(dir.FullName, "names.json"),
                resolvedPath: Path.Combine(dir.FullName, "handsup-resolved.json"),
                closedPath: Path.Combine(dir.FullName, "closed-desks.json"),
                alertsPath: alertsPath);

            var key = SessionStoreReader.AlertKey("workshop-product", "Need a decision", T0);
            Make().DismissAlert(key);
            Make().DismissAlert(key);   // idempotent — same key, no duplicate

            File.Exists(alertsPath).Should().BeTrue();
            Make().DismissedAlerts().Should().Contain(key);
            System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(alertsPath), "workshop-product").Count.Should().Be(1);
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void A_newer_signal_is_not_suppressed_by_an_old_dismissal()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            SessionStoreReader Make() => new(
                root: Path.Combine(dir.FullName, "no-sessions"),
                usageCache: Path.Combine(dir.FullName, "usage.json"),
                namesPath: Path.Combine(dir.FullName, "names.json"),
                resolvedPath: Path.Combine(dir.FullName, "handsup-resolved.json"),
                closedPath: Path.Combine(dir.FullName, "closed-desks.json"),
                alertsPath: Path.Combine(dir.FullName, "alerts-dismissed.json"));

            Make().DismissAlert(SessionStoreReader.AlertKey("d", "same reason", T0));
            var dismissed = Make().DismissedAlerts();

            dismissed.Should().Contain(SessionStoreReader.AlertKey("d", "same reason", T0));
            dismissed.Should().NotContain(SessionStoreReader.AlertKey("d", "same reason", T0.AddHours(1))); // a newer signal re-raises
        }
        finally { dir.Delete(recursive: true); }
    }
}
