using System.Threading;
using System.Windows.Forms;

namespace WorkshopRoom.Tray;

// The tray launcher: one thing you run. It starts the workshop web server
// hidden, drops an icon in the notification area, and opens the dashboard in
// your browser. Quit from the icon's menu and the web server stops with it.
internal static class Program
{
    // Held for the whole process lifetime so a second launch can detect us.
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main()
    {
        _singleInstance = new Mutex(initiallyOwned: true, "WorkshopRoom.Tray.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            MessageBox.Show(
                "The workshop is already running.\n\nLook for its icon in the notification area \u2014 click the ^ arrow near the clock if you don't see it.",
                "the workshop",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
    }
}
