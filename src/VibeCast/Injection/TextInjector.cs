using VibeCast.Config;
using VibeCast.Infrastructure;
using static VibeCast.Injection.NativeMethods;

namespace VibeCast.Injection;

/// <summary>
/// Writes text into whatever control currently has keyboard focus on the PC.
///
/// Default strategy is <b>Unicode keystroke injection</b> (SendInput with
/// KEYEVENTF_UNICODE): it is deterministic, needs no clipboard, does not clobber
/// the user's clipboard, and correctly handles CJK and emoji (surrogate pairs are
/// sent as separate UTF-16 code units, which SendInput reassembles). All events
/// are sent in a single atomic SendInput batch so nothing can interleave.
///
/// Clipboard-paste (Ctrl+V) is available as an opt-in fallback via
/// <see cref="AppConfig.UseClipboard"/> — faster for very long text and useful
/// for the rare control that ignores synthetic keystrokes. The paste path is
/// careful to restore the user's clipboard only *after* the paste has been
/// consumed (on a delayed background task), avoiding a restore-vs-paste race.
/// </summary>
public sealed class TextInjector
{
    private readonly AppConfig _config;

    public TextInjector(AppConfig config) => _config = config;

    /// <summary>
    /// Injects <paramref name="text"/> using the given mode. Safe to call from a
    /// background thread; the work is marshalled to the STA UI thread. Returns the
    /// number of characters written (0 if none), which the caller can hand back to
    /// <see cref="Undo"/> to remove them again.
    /// </summary>
    public int Inject(string text, InjectMode mode, bool submit)
    {
        if (string.IsNullOrEmpty(text) && !submit) return 0;

        return UiDispatcher.Invoke(() => InjectOnUiThread(text, mode, submit));
    }

    /// <summary>
    /// Removes the last <paramref name="length"/> characters this injector wrote,
    /// via plain Backspace. Note: in Dialog mode the original (replaced) content is
    /// not restored — this only removes what VibeCast itself typed.
    /// </summary>
    public void Undo(int length)
    {
        if (length <= 0) return;
        UiDispatcher.Invoke(() => UndoOnUiThread(length));
    }

    private int InjectOnUiThread(string text, InjectMode mode, bool submit)
    {
        int delay = Math.Clamp(_config.KeyDelayMs, 0, 500);
        int typedLength = 0;

        if (!string.IsNullOrEmpty(text))
        {
            // Dialog mode replaces the whole field: select-all first, then the new
            // text (typing or pasting) overwrites the selection.
            if (mode == InjectMode.Dialog)
            {
                SendCombo(VK_CONTROL, VK_A);
                Thread.Sleep(Math.Max(delay, 15));
            }

            if (_config.UseClipboard)
                PasteViaClipboard(text);
            else
                TypeUnicode(text);

            typedLength = text.Length;
        }

        if (submit)
        {
            Thread.Sleep(Math.Max(delay, 15));
            Send(KeyVirtual(VK_RETURN, false), KeyVirtual(VK_RETURN, true));
        }

        return typedLength;
    }

    private static void UndoOnUiThread(int length)
    {
        var inputs = new List<INPUT>(length * 2);
        for (int i = 0; i < length; i++)
        {
            inputs.Add(KeyVirtual(VK_BACK, false));
            inputs.Add(KeyVirtual(VK_BACK, true));
        }
        Send(inputs.ToArray());
    }

    private static void SendCombo(ushort modifier, ushort key)
    {
        Send(
            KeyVirtual(modifier, false),
            KeyVirtual(key, false),
            KeyVirtual(key, true),
            KeyVirtual(modifier, true));
    }

    /// <summary>Types the text as one atomic batch of Unicode key events.</summary>
    private static void TypeUnicode(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2 + 2);
        foreach (char c in text)
        {
            if (c == '\r') continue;
            if (c == '\n')
            {
                inputs.Add(KeyVirtual(VK_RETURN, false));
                inputs.Add(KeyVirtual(VK_RETURN, true));
            }
            else
            {
                inputs.Add(KeyUnicode(c, false));
                inputs.Add(KeyUnicode(c, true));
            }
        }
        if (inputs.Count > 0)
            Send(inputs.ToArray());
    }

    // --- Optional clipboard-paste path ---

    private void PasteViaClipboard(string text)
    {
        string? saved = TryGetClipboardText();
        if (!TrySetClipboardText(text))
        {
            // Clipboard unavailable -> fall back to typing so we never silently fail.
            TypeUnicode(text);
            return;
        }

        SendCombo(VK_CONTROL, VK_V);

        // Restore the user's clipboard only after the paste is surely consumed.
        // Doing it on a delayed background task avoids racing the async keystroke.
        _ = Task.Run(async () =>
        {
            await Task.Delay(700);
            UiDispatcher.Invoke(() => RestoreClipboard(saved));
        });
    }

    // --- Clipboard helpers (with a few retries; the clipboard can be briefly locked) ---

    private static string? TryGetClipboardText()
    {
        for (int i = 0; i < 5; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch { Thread.Sleep(20); }
        }
        return null;
    }

    private static bool TrySetClipboardText(string text)
    {
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetText(text); return true; }
            catch { Thread.Sleep(20); }
        }
        return false;
    }

    private static void RestoreClipboard(string? saved)
    {
        try
        {
            if (string.IsNullOrEmpty(saved)) Clipboard.Clear();
            else Clipboard.SetText(saved);
        }
        catch { /* best effort */ }
    }
}
