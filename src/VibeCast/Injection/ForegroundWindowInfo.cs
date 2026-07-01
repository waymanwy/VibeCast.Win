using System.Runtime.InteropServices;
using System.Text;

namespace VibeCast.Injection;

/// <summary>Reads the title of whatever window currently has focus on the PC,
/// so the phone can show the user where their text is about to land.</summary>
internal static class ForegroundWindowInfo
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>The foreground window's title, or null if there isn't one, it has
    /// no title, or it belongs to VibeCast itself (tray/connection windows aren't
    /// meaningful injection targets).</summary>
    public static string? GetTitle()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Environment.ProcessId) return null;

        int len = GetWindowTextLength(hwnd);
        if (len <= 0) return null;

        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        string title = sb.ToString().Trim();
        return title.Length == 0 ? null : title;
    }
}
