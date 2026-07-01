using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VibeCast.Injection;

namespace VibeCast.Server;

/// <summary>
/// Handles all connected phones: dispatches inject/undo requests to the
/// <see cref="TextInjector"/>, and periodically broadcasts the PC's current
/// foreground-window title so the phone can show "typing into: X" before the
/// user commits to sending anything.
/// </summary>
public sealed class WebSocketHub : IDisposable
{
    private readonly TextInjector _injector;
    private readonly ConcurrentDictionary<WebSocket, byte> _sockets = new();
    private readonly System.Threading.Timer _targetPollTimer;

    // Sentinel that never equals a real title (including null), so the first
    // poll after startup always broadcasts once.
    private string? _lastBroadcastTitle = "\0";

    private readonly object _undoLock = new();
    private int _lastInjectedLength;

    /// <summary>Raised (on a background thread) after a successful injection, with
    /// a short human-readable description. Used for the tray balloon.</summary>
    public event Action<string>? Injected;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public int ClientCount => _sockets.Count;

    public event Action? ClientsChanged;

    public WebSocketHub(TextInjector injector)
    {
        _injector = injector;
        _targetPollTimer = new System.Threading.Timer(_ => PollForegroundWindow(), null, 500, 800);
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        _sockets[socket] = 0;
        ClientsChanged?.Invoke();

        // Greet with the PC's current focus.
        await SendJsonAsync(socket, new TargetInfoMessage { Title = ForegroundWindowInfo.GetTitle() }, ct);

        var buffer = new byte[16 * 1024];
        var sb = new StringBuilder();

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                await DispatchAsync(socket, sb.ToString(), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { /* client dropped */ }
        finally
        {
            _sockets.TryRemove(socket, out _);
            ClientsChanged?.Invoke();
        }
    }

    private async Task DispatchAsync(WebSocket socket, string json, CancellationToken ct)
    {
        InboundMessage? msg;
        try { msg = JsonSerializer.Deserialize<InboundMessage>(json, JsonOpts); }
        catch { await AckAsync(socket, false, "bad json", null, ct); return; }

        if (msg is null) { await AckAsync(socket, false, "empty", null, ct); return; }

        switch (msg.Type)
        {
            case "hello":
                await SendJsonAsync(socket, new TargetInfoMessage { Title = ForegroundWindowInfo.GetTitle() }, ct);
                break;

            case "inject":
                await HandleInjectAsync(socket, msg, ct);
                break;

            case "undo":
                await HandleUndoAsync(socket, ct);
                break;

            default:
                await AckAsync(socket, false, "unknown type", null, ct);
                break;
        }
    }

    private async Task HandleInjectAsync(WebSocket socket, InboundMessage msg, CancellationToken ct)
    {
        string text = msg.Text ?? "";
        InjectMode mode = ParseMode(msg.Mode) ?? InjectMode.Editor;
        bool submit = msg.Submit ?? false;

        if (string.IsNullOrEmpty(text) && !submit)
        {
            await AckAsync(socket, false, "nothing to send", null, ct);
            return;
        }

        // Capture what's actually focused right now, not just the requested target's
        // display name — this is the ground truth the phone shows back to the user.
        string? actualTarget = ForegroundWindowInfo.GetTitle();

        try
        {
            int typedLength = _injector.Inject(text, mode, submit);

            // Pressing Enter (submit) likely already "commits" the text (e.g. sends a
            // chat message), so backspacing afterwards would do more harm than good —
            // only offer undo when nothing was submitted.
            lock (_undoLock) { _lastInjectedLength = submit ? 0 : typedLength; }

            Injected?.Invoke(Preview(text));
            await AckAsync(socket, true, null, actualTarget, ct);
        }
        catch (Exception ex)
        {
            await AckAsync(socket, false, ex.Message, null, ct);
        }
    }

    private async Task HandleUndoAsync(WebSocket socket, CancellationToken ct)
    {
        int length;
        lock (_undoLock)
        {
            length = _lastInjectedLength;
            _lastInjectedLength = 0;
        }

        if (length <= 0)
        {
            await SendJsonAsync(socket, new UndoAckMessage { Ok = false, Message = "nothing to undo" }, ct);
            return;
        }

        try
        {
            _injector.Undo(length);
            await SendJsonAsync(socket, new UndoAckMessage { Ok = true }, ct);
        }
        catch (Exception ex)
        {
            await SendJsonAsync(socket, new UndoAckMessage { Ok = false, Message = ex.Message }, ct);
        }
    }

    private void PollForegroundWindow()
    {
        if (_sockets.IsEmpty) return;

        string? title = ForegroundWindowInfo.GetTitle();
        if (title == _lastBroadcastTitle) return;
        _lastBroadcastTitle = title;

        var payload = new TargetInfoMessage { Title = title };
        foreach (var socket in _sockets.Keys)
            _ = SendJsonAsync(socket, payload, CancellationToken.None);
    }

    private static InjectMode? ParseMode(string? mode) => mode?.ToLowerInvariant() switch
    {
        "editor" => InjectMode.Editor,
        "dialog" => InjectMode.Dialog,
        _ => null
    };

    private static string Preview(string text)
    {
        text = text.Replace("\n", " ").Trim();
        return text.Length <= 40 ? text : text[..40] + "…";
    }

    private static Task AckAsync(WebSocket socket, bool ok, string? message, string? target, CancellationToken ct)
        => SendJsonAsync(socket, new AckMessage { Ok = ok, Message = message, Target = target }, ct);

    /// <summary>Sends a one-off error frame to a socket that hasn't been handed to
    /// <see cref="HandleAsync"/> yet (used by <c>WebServer</c> to reject a failed
    /// pairing-token check with a message the client can show, before closing).</summary>
    public static Task SendErrorAsync(WebSocket socket, string message, CancellationToken ct)
        => SendJsonAsync(socket, new ErrorMessage { Message = message }, ct);

    private static async Task SendJsonAsync<T>(WebSocket socket, T payload, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        catch (WebSocketException) { /* client dropped mid-send */ }
    }

    public void Dispose() => _targetPollTimer.Dispose();
}
