using System.Security.Cryptography;

namespace VibeCast.Config;

/// <summary>Persisted application settings. A single instance is shared across
/// the tray UI and the web server, so live edits from the config page take
/// effect immediately.</summary>
public sealed class AppConfig
{
    /// <summary>HTTP port the local server listens on.</summary>
    public int Port { get; set; } = 8443;

    /// <summary>
    /// Random secret embedded in the QR code / connection URL as ?token=.
    /// The server rejects any WebSocket connection or /api/config request that
    /// doesn't present it, so a device merely being on the same Wi-Fi is not
    /// enough to control this PC. Generated once and persisted; regenerate by
    /// deleting config.json (or later, an "Reset pairing" tray action).
    /// </summary>
    public string PairingToken { get; set; } = GenerateToken();

    /// <summary>Milliseconds to wait between simulated keystrokes. Increase if a
    /// slow app drops characters.</summary>
    public int KeyDelayMs { get; set; } = 25;

    /// <summary>Show a tray balloon after each successful injection.</summary>
    public bool NotifyOnInject { get; set; } = true;

    /// <summary>
    /// Use clipboard-paste (Ctrl+V) instead of typing Unicode keystrokes.
    /// Off by default: typing is deterministic and never touches the clipboard.
    /// Turn on for very long text or apps that ignore synthetic keystrokes.
    /// </summary>
    public bool UseClipboard { get; set; } = false;

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        // URL-safe, no padding — goes straight into a query string / QR code.
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');
    }
}
