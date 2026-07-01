# VibeCast WebSocket protocol

All frames are UTF-8 JSON text over a single WebSocket at
`ws://<pc>:<port>/ws?token=<pairing token>`.

## Authentication

The `token` query parameter is required and must match the PC's
`AppConfig.PairingToken` (shown embedded in the QR code / connection URL).
If missing or wrong, the server accepts the socket just long enough to send
one `error` frame with `message: "unauthorized"`, then closes it.

The REST endpoints (`/api/config`) require the same token in an
`X-VibeCast-Token` header instead.

## Client → Server

### `hello`
Sent right after connect. Server replies with a `target` frame.
```json
{ "type": "hello", "client": "<user agent>" }
```

### `inject`
Ask the PC to type text into whatever control currently has focus. There is
no target/app selection — the client always states exactly how it wants the
text delivered.
```json
{
  "type": "inject",
  "text": "the text to type",
  "mode": "editor|dialog",   // optional, defaults to "editor"
  "submit": true              // optional, defaults to false (press Enter after)
}
```
`mode` **editor** → insert at caret (safe for any control). **dialog** →
select-all then replace (for prefilled chat/prompt boxes).

### `undo`
Remove the text from the most recent `inject` (via Backspace). No-op if the
last inject used `submit: true`, or if nothing has been injected since the
last undo.
```json
{ "type": "undo" }
```

## Server → Client

### `target`
The PC's current foreground window title, so the phone can show "typing
into: X" before the user sends anything. Pushed on connect, on `hello`, and
whenever the PC's focus changes (polled ~every 800ms). `title` is `null` if
unknown or if VibeCast's own windows are focused.
```json
{ "type": "target", "title": "Untitled - Notepad" }
```

### `ack`
Result of the last `inject`. `target` is the window title actually typed
into, captured at the moment of injection.
```json
{ "type": "ack", "ok": true, "message": null, "target": "Untitled - Notepad" }
```

### `undoAck`
Result of the last `undo`.
```json
{ "type": "undoAck", "ok": true, "message": null }
```

### `error`
Out-of-band failure not tied to a specific request (currently just failed
pairing-token checks on connect).
```json
{ "type": "error", "message": "unauthorized" }
```

## REST (config page)

Requires header `X-VibeCast-Token: <pairing token>` on every request.

- `GET  /api/config` → `{ port, notifyOnInject, keyDelayMs }` (401 if unauthorized)
- `POST /api/config` with `{ notifyOnInject, keyDelayMs }` → `{ ok: true }` (401 if unauthorized)
