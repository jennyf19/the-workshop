using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace WorkshopRoom.Tray;

/// <summary>
/// The tray application: a NotifyIcon with a small menu that supervises the
/// hidden web server. No console window — you open the dashboard in your
/// browser and quit from the menu, which stops the web server with it.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly SynchronizationContext _ui;
    private readonly WebHostProcess _host;

    public TrayApplicationContext()
    {
        // Captured on the WinForms UI thread so the web child's exit callback
        // (raised on a thread-pool thread) can marshal back to touch the icon.
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _icon = TrayIconFactory.Create();

        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open the workshop", null, (_, _) => OpenBrowser())
        {
            Font = new Font(SystemFonts.MenuFont ?? SystemFonts.DefaultFont, FontStyle.Bold),
        };
        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "the workshop",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenBrowser();

        _host = new WebHostProcess();
        _host.ExitedUnexpectedly += OnWebExitedUnexpectedly;

        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            _host.Start();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            ExitThread();
            return;
        }

        // No ConfigureAwait(false): resume on the UI thread so the icon calls
        // below are safe.
        var ready = await _host.WaitForReadyAsync(TimeSpan.FromSeconds(30));
        if (ready)
        {
            _notifyIcon.ShowBalloonTip(3000, "the workshop", "Running \u2014 click the icon to open it.", ToolTipIcon.Info);
            OpenBrowser();
        }
        else
        {
            _notifyIcon.ShowBalloonTip(5000, "the workshop",
                "The server didn't come up. Check the log in %LocalAppData%\\the-workshop\\logs.", ToolTipIcon.Warning);
        }
    }

    private void OpenBrowser()
    {
        try { Process.Start(new ProcessStartInfo { FileName = _host.BaseUrl, UseShellExecute = true }); }
        catch { /* no default browser — nothing we can do */ }
    }

    private void OnWebExitedUnexpectedly() =>
        _ui.Post(_ =>
        {
            try { _notifyIcon.ShowBalloonTip(4000, "the workshop", "The server stopped \u2014 exiting.", ToolTipIcon.Error); }
            catch { /* icon already gone */ }
            ExitThread();
        }, null);

    protected override void ExitThreadCore()
    {
        try { _notifyIcon.Visible = false; } catch { }
        try { _host.Stop(); } catch { }
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _host.Dispose(); } catch { }
            try { _notifyIcon.Dispose(); } catch { }
            try { _icon.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "the workshop", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
