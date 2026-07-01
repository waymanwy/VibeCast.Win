namespace VibeCast.Infrastructure;

/// <summary>
/// Marshals work from background threads (the web server) onto the WinForms
/// STA UI thread. Clipboard access and reliable SendInput both prefer to run on
/// that thread while the message pump (Application.Run) is active.
/// </summary>
public static class UiDispatcher
{
    private static Form? _pump;

    public static void Initialize()
    {
        _pump = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            Size = new Size(1, 1),
            Opacity = 0
        };
        // Force handle creation on the UI thread without ever showing the form.
        _ = _pump.Handle;
    }

    public static void Invoke(Action action)
    {
        var pump = _pump ?? throw new InvalidOperationException("UiDispatcher not initialized.");
        if (pump.InvokeRequired)
            pump.Invoke(action);
        else
            action();
    }

    public static T Invoke<T>(Func<T> func)
    {
        var pump = _pump ?? throw new InvalidOperationException("UiDispatcher not initialized.");
        return pump.InvokeRequired ? (T)pump.Invoke(func) : func();
    }
}
