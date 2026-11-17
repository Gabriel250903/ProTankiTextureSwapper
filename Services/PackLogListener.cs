using Serilog;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class PackLogListener(ISettingsService settingsService) : IDisposable
    {
        private const string Prefix = "http://127.0.0.1:47921/";
        private static readonly byte[] TransparentGif =
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
            0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00,
            0x00, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02,
            0x44, 0x01, 0x00, 0x3B,
        ];

        private readonly ISettingsService _settingsService = settingsService;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly object _sync = new();

        private static string? _pendingUpdateMessage;

        public static string? PendingUpdateMessage
        {
            get => Volatile.Read(ref _pendingUpdateMessage);
            set => Volatile.Write(ref _pendingUpdateMessage, value);
        }

        public event Action<string>? MenuHotkeyChanged;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            lock (_sync)
            {
                if (IsRunning)
                {
                    return;
                }

                try
                {
                    _cts = new CancellationTokenSource();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(Prefix);
                    _listener.Start();
                    IsRunning = true;
                    CancellationToken token = _cts.Token;
                    HttpListener listener = _listener;
                    _ = Task.Run(() => ListenLoopAsync(listener, token));
                    Log.Information("ProTanki Pack log listener started on {Prefix}", Prefix);
                }
                catch (Exception ex)
                {
                    IsRunning = false;
                    Log.Warning(ex, "Could not start ProTanki Pack log listener on {Prefix}", Prefix);
                    StopInternal();
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (!IsRunning && _listener == null)
                {
                    return;
                }
                StopInternal();
                Log.Information("ProTanki Pack log listener stopped.");
            }
        }

        private void StopInternal()
        {
            IsRunning = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _listener = null;
            _cts = null;
        }

        private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
        }

        private static bool IsAllowedOrigin(string? origin)
        {
            return string.IsNullOrWhiteSpace(origin) || string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) && (uri.Host.Equals("play-protanki.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".play-protanki.com", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAuthorizedRequest(HttpListenerRequest request, string? fallbackToken = null)
        {
            string expected = _settingsService.Load().PackIpcToken;
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            string provided = request.Headers["X-PTPack-Token"] ?? fallbackToken ?? string.Empty;
            return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(expected)
            );
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            try
            {
                string? origin = ctx.Request.Headers["Origin"];
                if (!IsAllowedOrigin(origin))
                {
                    ctx.Response.StatusCode = 403;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(origin))
                {
                    ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
                    ctx.Response.Headers["Vary"] = "Origin";
                }
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-PTPack-Token";

                if (ctx.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                if (
                    ctx.Request.Url?.AbsolutePath.Equals("/ptpack-status", StringComparison.OrdinalIgnoreCase) == true
                )
                {
                    if (!IsAuthorizedRequest(ctx.Request))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }
                    string? pendingMsg = PendingUpdateMessage;
                    string json = JsonSerializer.Serialize(
                        new
                        {
                            pendingUpdate = !string.IsNullOrEmpty(pendingMsg),
                            message = pendingMsg ?? string.Empty,
                        }
                    );
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else if (
                    ctx.Request.Url?.AbsolutePath.Equals("/ptpack-log", StringComparison.OrdinalIgnoreCase) == true
                )
                {
                    if (!IsAuthorizedRequest(ctx.Request, ctx.Request.QueryString["token"]))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }
                    string level = ctx.Request.QueryString["level"] ?? "error";
                    string message = ctx.Request.QueryString["msg"] ?? string.Empty;

                    if (
                        string.IsNullOrEmpty(message)
                        && ctx.Request.HasEntityBody
                        && ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        using StreamReader reader = new(ctx.Request.InputStream, Encoding.UTF8);
                        string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(body);
                                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                                {
                                    if (doc.RootElement.TryGetProperty("level", out JsonElement lvlEl))
                                    {
                                        level = lvlEl.GetString() ?? level;
                                    }
                                    if (doc.RootElement.TryGetProperty("msg", out JsonElement msgEl))
                                    {
                                        message = msgEl.GetString() ?? body;
                                    }
                                }
                                else
                                {
                                    message = body;
                                }
                            }
                            catch
                            {
                                message = body;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(message) && _settingsService.Load().EnablePackLogListener)
                    {
                        if (message.Length > 2000)
                        {
                            message = message[..2000];
                        }

                        switch (level.ToLowerInvariant())
                        {
                            case "warn":
                            case "warning":
                                Log.Warning("[Client/ProTanki Pack] {Message}", message);
                                break;
                            case "info":
                            case "log":
                                Log.Information("[Client/ProTanki Pack] {Message}", message);
                                break;
                            default:
                                Log.Error("[Client/ProTanki Pack] {Message}", message);
                                break;
                        }
                    }

                    if (ctx.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "image/gif";
                        ctx.Response.OutputStream.Write(TransparentGif, 0, TransparentGif.Length);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 204;
                    }
                }
                else if (
                    ctx.Request.Url?.AbsolutePath.Equals("/ptpack-hotkey", StringComparison.OrdinalIgnoreCase) == true
                )
                {
                    if (!ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 405;
                        return;
                    }

                    if (!IsAuthorizedRequest(ctx.Request))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }

                    string requestedKey = string.Empty;

                    if (ctx.Request.HasEntityBody)
                    {
                        using StreamReader reader = new(ctx.Request.InputStream, Encoding.UTF8);
                        string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(body);
                                if (
                                    doc.RootElement.ValueKind == JsonValueKind.Object
                                    && doc.RootElement.TryGetProperty("hotkey", out JsonElement hotkeyEl)
                                )
                                {
                                    requestedKey = hotkeyEl.GetString() ?? string.Empty;
                                }
                            }
                            catch
                            {
                                requestedKey = body.Trim();
                            }
                        }
                    }

                    if (!Constants.IsValidShaderHotkey(requestedKey))
                    {
                        ctx.Response.StatusCode = 400;
                        return;
                    }

                    string normalized = Constants.NormalizeShaderHotkey(requestedKey);
                    AppSettings settings = _settingsService.Load();
                    if (!string.Equals(settings.ProTankiPackHotkey, normalized, StringComparison.Ordinal))
                    {
                        settings.ProTankiPackHotkey = normalized;
                        _settingsService.Save(settings);
                        Log.Information("[Client/ProTanki Pack] ProTanki Pack menu hotkey synced from client: {Hotkey}", normalized);
                        MenuHotkeyChanged?.Invoke(normalized);
                    }

                    string json = JsonSerializer.Serialize(new { ok = true, hotkey = normalized });
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else if (
                    ctx.Request.Url?.AbsolutePath.Equals("/ptpack-logs", StringComparison.OrdinalIgnoreCase) == true
                )
                {
                    if (!IsAuthorizedRequest(ctx.Request))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }

                    string fileName = string.Empty;
                    string content;
                    try
                    {
                        string logPath = App.CurrentLogFilePath;
                        if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                        {
                            fileName = Path.GetFileName(logPath);
                            using FileStream fs = new(
                                logPath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite
                            );
                            const long maxBytes = 100_000;
                            bool truncated = fs.Length > maxBytes;
                            if (truncated)
                            {
                                _ = fs.Seek(-maxBytes, SeekOrigin.End);
                            }

                            using StreamReader reader = new(fs, Encoding.UTF8);
                            content = reader.ReadToEnd();
                            if (truncated)
                            {
                                int firstNl = content.IndexOf('\n');
                                if (firstNl >= 0 && firstNl + 1 < content.Length)
                                {
                                    content = content[(firstNl + 1)..];
                                }
                            }
                        }
                        else
                        {
                            content = "No log file found.";
                        }
                    }
                    catch (Exception ex)
                    {
                        content = "Error reading log file: " + ex.Message;
                    }

                    string json = JsonSerializer.Serialize(new { ok = true, file = fileName, content });
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers["Cache-Control"] = "no-store";
                    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal();
            }
        }
    }
}
