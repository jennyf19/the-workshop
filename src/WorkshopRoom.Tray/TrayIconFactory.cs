using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WorkshopRoom.Tray;

/// <summary>
/// Builds the notification-area icon at runtime so the project ships no binary
/// .ico asset. A rounded dark-purple square with the workshop's cairn mark —
/// three stacked white stones — rendered at 32x32 (Windows down-samples for the
/// 16px tray slot). Purple so it stands out among the usual tray icons.
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(ColorTranslator.FromHtml("#6D28D9"));   // dark purple — stands out in the tray
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7);
            g.FillPath(bg, path);

            using var fg = new SolidBrush(Color.White);
            // The cairn — three stacked stones (matches favicon.svg and the h1
            // mark). GDI+ FillEllipse takes the bounding box (x, y, w, h);
            // these are the SVG ellipses (cx,cy,rx,ry) converted to boxes.
            g.FillEllipse(fg, 6.0f, 21.6f, 20.0f, 6.8f);   // bottom
            g.FillEllipse(fg, 9.6f, 14.1f, 14.4f, 6.2f);   // middle
            g.FillEllipse(fg, 10.7f, 7.1f, 9.2f, 5.4f);    // top
        }

        var hicon = bmp.GetHicon();
        try
        {
            // Clone() deep-copies into a self-contained managed Icon, so the
            // native HICON can be destroyed immediately (no leak, no lifetime
            // coupling to the NotifyIcon).
            return (Icon)Icon.FromHandle(hicon).Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
