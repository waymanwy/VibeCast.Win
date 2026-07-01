using VibeCast.Config;
using VibeCast.Infrastructure;
using VibeCast.Server;
using VibeCast.UI;

namespace VibeCast;

internal static class Program
{
    /// <summary>
    /// Entry point. Runs as a system-tray application that hosts a local
    /// HTTP + WebSocket server. A phone on the same network opens the served
    /// page (using its own keyboard's dictation, not the browser's), and the
    /// text is injected into whatever input box currently has focus on the PC.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Ensure only one instance runs (the port would clash otherwise).
        using var single = new Mutex(true, "VibeCast.SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("VibeCast is already running (see the system tray).",
                "VibeCast", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Marshaller so background server threads can run clipboard / SendInput
        // work on the STA UI thread.
        UiDispatcher.Initialize();

        AppConfig config = ConfigStore.Load();

        var server = new WebServer(config);
        try
        {
            server.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start the local server on port {config.Port}.\n\n{ex.Message}\n\n" +
                "The port may be in use. Change it via the config page and restart.",
                "VibeCast", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        using var tray = new TrayApplicationContext(config, server);
        Application.Run(tray);

        server.StopAsync().GetAwaiter().GetResult();
    }
}
