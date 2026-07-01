using System.Text.Json.Serialization;

namespace VibeCast.Server;

/// <summary>
/// WebSocket message contract between the phone (client) and the PC (server).
/// All frames are JSON text. See shared/protocol.md for the human description.
/// </summary>

// ---- Client -> Server ----

public sealed class InboundMessage
{
    /// <summary>"inject" | "hello" | "undo"</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "";

    [JsonPropertyName("text")] public string? Text { get; set; }

    /// <summary>"editor" (insert at caret) or "dialog" (select-all then replace). Defaults to "editor".</summary>
    [JsonPropertyName("mode")] public string? Mode { get; set; }

    /// <summary>Press Enter after inserting. Defaults to false.</summary>
    [JsonPropertyName("submit")] public bool? Submit { get; set; }
}

// ---- Server -> Client ----

public sealed class AckMessage
{
    [JsonPropertyName("type")] public string Type => "ack";
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }

    /// <summary>The PC window title the text was actually typed into, captured at
    /// the moment of injection (not just the requested target's display name).</summary>
    [JsonPropertyName("target")] public string? Target { get; set; }
}

public sealed class ErrorMessage
{
    [JsonPropertyName("type")] public string Type => "error";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

/// <summary>Current PC foreground-window title, pushed so the phone can show
/// "typing into: X" before the user sends anything.</summary>
public sealed class TargetInfoMessage
{
    [JsonPropertyName("type")] public string Type => "target";
    [JsonPropertyName("title")] public string? Title { get; set; }
}

public sealed class UndoAckMessage
{
    [JsonPropertyName("type")] public string Type => "undoAck";
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}
