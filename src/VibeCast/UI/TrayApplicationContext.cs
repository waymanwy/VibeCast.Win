using Microsoft.Win32;
using VibeCast.Config;
using VibeCast.Infrastructure;
using VibeCast.Server;

namespace VibeCast.UI;

/// <summary>The system-tray presence: icon, context menu, and inject balloons.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VibeCast";

    private readonly AppConfig _config;
    private readonly WebServer _server;
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;
    private ConnectionForm? _connectionForm;

    public TrayApplicationContext(AppConfig config, WebServer server)
    {
        _config = config;
        _server = server;

        _icon = CreateTrayIcon();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "VibeCast",
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => ShowConnectionInfo();

        _server.Hub.Injected += OnInjected;
        _server.Hub.ClientsChanged += UpdateTooltip;
        UpdateTooltip();

        ShowStartupBalloon();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Connection info…", null, (_, _) => ShowConnectionInfo());
        menu.Items.Add("Open config page", null, (_, _) => OpenInBrowser("config.html"));
        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = IsAutostartEnabled()
        };
        autostart.CheckedChanged += (_, _) => SetAutostart(autostart.Checked);
        menu.Items.Add(autostart);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        return menu;
    }

    private void UpdateTooltip()
    {
        // Fired from background WebSocket threads -> marshal to the UI thread.
        UiDispatcher.Invoke(() =>
        {
            int clients = _server.Hub.ClientCount;
            string text = $"VibeCast · port {_server.Port} · {clients} phone(s) connected";
            // NotifyIcon.Text is capped at 63 chars.
            _tray.Text = text.Length <= 63 ? text : text[..63];
        });
    }

    private void OnInjected(string preview)
    {
        if (!_config.NotifyOnInject) return;
        UiDispatcher.Invoke(() =>
        {
            try
            {
                _tray.BalloonTipTitle = "VibeCast — text sent";
                _tray.BalloonTipText = preview;
                _tray.ShowBalloonTip(1500);
            }
            catch { /* balloons are best-effort */ }
        });
    }

    private void ShowStartupBalloon()
    {
        var url = _server.ConnectionUrls().FirstOrDefault() ?? $"http://localhost:{_server.Port}/";
        _tray.BalloonTipTitle = "VibeCast is running";
        _tray.BalloonTipText = $"Open {url} on your phone. Double-click the tray icon for the QR code.";
        _tray.ShowBalloonTip(4000);
    }

    private void ShowConnectionInfo()
    {
        if (_connectionForm is null || _connectionForm.IsDisposed)
            _connectionForm = new ConnectionForm(_server);

        _connectionForm.Show();
        _connectionForm.WindowState = FormWindowState.Normal;
        _connectionForm.BringToFront();
        _connectionForm.Activate();
    }

    private void OpenInBrowser(string relativePath)
    {
        // Opened on the PC itself, so localhost + the pairing token (rather than
        // one of the LAN URLs) is always correct here.
        string url = $"http://localhost:{_server.Port}/{relativePath.TrimStart('/')}?token={_server.PairingToken}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch { /* no default browser */ }
    }

    // --- Autostart via the per-user Run registry key ---

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(RunValueName) is not null;
    }

    private static void SetAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null) return;

        if (enabled)
            key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(RunValueName, false);
    }

    private void ExitApp()
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _icon.Dispose();
            _connectionForm?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Draws a simple round mic/broadcast glyph so we don't ship a binary .ico.</summary>
    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(Color.FromArgb(94, 92, 230)); // indigo
            g.FillEllipse(bg, 1, 1, 30, 30);

            using var fg = new SolidBrush(Color.White);
            // mic capsule
            g.FillRoundedRectangle(fg, new Rectangle(13, 7, 6, 12), 3);
            // mic stand arc
            using var pen = new Pen(Color.White, 2f);
            g.DrawArc(pen, 9, 9, 14, 14, 20, 140);
            g.DrawLine(pen, 16, 22, 16, 26);
            g.DrawLine(pen, 12, 26, 20, 26);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}

/// <summary>Small drawing helper for rounded rectangles (used by the tray icon).</summary>
internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle r, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
