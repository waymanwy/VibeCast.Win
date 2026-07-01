using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeCast.Config;
using VibeCast.Injection;

namespace VibeCast.Server;

/// <summary>
/// Hosts the local site over plain HTTP: static mobile UI, a small REST config
/// API, and the /ws WebSocket endpoint. Deliberately not HTTPS — the phone never
/// uses its own microphone (voice comes from the phone's keyboard, outside the
/// page), so there's no "secure context" requirement, and skipping TLS avoids
/// the self-signed certificate warning entirely. Access is instead gated by a
/// pairing token (see <see cref="AppConfig.PairingToken"/>).
/// Runs Kestrel on a background task so the WinForms message pump stays responsive.
/// </summary>
public sealed class WebServer
{
    private readonly AppConfig _config;
    private WebApplication? _app;

    public WebSocketHub Hub { get; }
    public int Port => _config.Port;
    public string PairingToken => _config.PairingToken;

    public WebServer(AppConfig config)
    {
        _config = config;
        Hub = new WebSocketHub(new TextInjector(config));
    }

    /// <summary>Candidate URLs (with the pairing token) the phone can open, one per LAN address.</summary>
    public IReadOnlyList<string> ConnectionUrls()
    {
        var urls = Net.NetworkInfo.LocalIPv4Addresses()
            .Select(ip => $"http://{ip}:{_config.Port}/?token={_config.PairingToken}")
            .ToList();
        if (urls.Count == 0)
            urls.Add($"http://localhost:{_config.Port}/?token={_config.PairingToken}");
        return urls;
    }

    public void Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Logging.ClearProviders(); // no console in a WinExe

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, _config.Port);
        });

        var app = builder.Build();

        // The mobile UI is embedded in the assembly, not on disk.
        var webFiles = new ManifestEmbeddedFileProvider(typeof(WebServer).Assembly, "wwwroot");

        app.UseWebSockets();
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFiles }); // "/" -> index.html
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = webFiles,
            // Phones (and stubborn in-app browsers) otherwise cache the UI and
            // never pick up updates. The payload is tiny and embedded, so just
            // tell clients never to cache it — always revalidate against us.
            OnPrepareResponse = ctx =>
            {
                var headers = ctx.Context.Response.Headers;
                headers.CacheControl = "no-store, no-cache, must-revalidate";
                headers.Pragma = "no-cache";
                headers.Expires = "0";
            }
        });

        MapApi(app);
        MapWebSocket(app);

        _app = app;
        // Fire and forget; exceptions surface via the returned task's continuation.
        _ = app.RunAsync();
    }

    /// <summary>Header the config page sends the pairing token in.</summary>
    private const string TokenHeader = "X-VibeCast-Token";

    private bool IsAuthorized(string? presented) =>
        !string.IsNullOrEmpty(presented) && presented == _config.PairingToken;

    private void MapApi(WebApplication app)
    {
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = false };

        // Behaviour settings for the config page.
        app.MapGet("/api/config", (HttpRequest req) =>
        {
            if (!IsAuthorized(req.Headers[TokenHeader]))
                return Results.Json(new { error = "unauthorized" }, jsonOpts, statusCode: StatusCodes.Status401Unauthorized);

            return Results.Json(new { port = _config.Port,
                notifyOnInject = _config.NotifyOnInject, keyDelayMs = _config.KeyDelayMs }, jsonOpts);
        });

        // Save behaviour settings from the config page.
        app.MapPost("/api/config", async (HttpRequest req) =>
        {
            if (!IsAuthorized(req.Headers[TokenHeader]))
                return Results.Json(new { ok = false, error = "unauthorized" }, jsonOpts, statusCode: StatusCodes.Status401Unauthorized);

            ConfigUpdate? update;
            try
            {
                update = await JsonSerializer.DeserializeAsync<ConfigUpdate>(req.Body, jsonOpts);
            }
            catch
            {
                return Results.BadRequest(new { ok = false, error = "invalid json" });
            }

            if (update is null)
                return Results.BadRequest(new { ok = false, error = "empty body" });

            if (update.NotifyOnInject is bool n)
                _config.NotifyOnInject = n;
            if (update.KeyDelayMs is int d)
                _config.KeyDelayMs = Math.Clamp(d, 0, 500);

            ConfigStore.Save(_config);
            return Results.Json(new { ok = true }, jsonOpts);
        });
    }

    private void MapWebSocket(WebApplication app)
    {
        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();

            if (!IsAuthorized(ctx.Request.Query["token"]))
            {
                await WebSocketHub.SendErrorAsync(socket, "unauthorized", ctx.RequestAborted);
                await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation, "unauthorized", ctx.RequestAborted);
                return;
            }

            await Hub.HandleAsync(socket, ctx.RequestAborted);
        });
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            try { await _app.StopAsync(TimeSpan.FromSeconds(2)); }
            catch { /* shutting down */ }
            await _app.DisposeAsync();
            _app = null;
            Hub.Dispose();
        }
    }

    private sealed class ConfigUpdate
    {
        public bool? NotifyOnInject { get; set; }
        public int? KeyDelayMs { get; set; }
    }
}
