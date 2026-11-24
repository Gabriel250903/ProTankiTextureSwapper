using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class ChatService : IChatService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _disposed;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public bool HasAccess { get; private set; }
        public bool IsSpecialThemeAllowed { get; private set; }
        public bool IsAdmin { get; private set; }
        public static bool IsAdminDevice { get; private set; }

        public event EventHandler<ChatMessage>? MessageReceived;
        public event EventHandler<IReadOnlyList<ChatMessage>>? HistoryReceived;
        public event EventHandler<(int Snowflakes, int Streak, string Badge)>? StatsUpdated;
        public event EventHandler<IReadOnlyList<string>>? UnlockedBadgesUpdated;
        public event EventHandler<string>? GrandPrizeReceived;
        public event EventHandler? SkinVoucherWon;
        public event EventHandler<bool>? ConnectionStateChanged;

        public async Task<bool> CheckAccessAsync()
        {
            if (!IntegrityChecker.ValidateAssemblyIntegrity())
            {
                HasAccess = false;
                IsAdmin = false;
                IsAdminDevice = false;
                return false;
            }

            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                string url = $"{Constants.GatekeeperWorkerUrl}/chat/access";
                using HttpRequestMessage req = new(HttpMethod.Get, url);
                _ = req.Headers.TryAddWithoutValidation(
                    "X-Api-Key",
                    Constants.GatekeeperClientAppId
                );
                _ = req.Headers.TryAddWithoutValidation("X-HWID", hwid);

                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    HasAccess = false;
                    IsAdmin = false;
                    IsAdminDevice = false;
                    return false;
                }

                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                bool allowed =
                    doc.RootElement.TryGetProperty("allowed", out JsonElement allowedEl)
                    && allowedEl.GetBoolean();
                bool isAdmin =
                    doc.RootElement.TryGetProperty("isAdmin", out JsonElement adminEl)
                    && adminEl.ValueKind == JsonValueKind.True;
                bool specialThemeAllowed =
                    doc.RootElement.TryGetProperty(
                        "specialThemeAllowed",
                        out JsonElement stAllowedEl
                    )
                    && stAllowedEl.ValueKind == JsonValueKind.True;
                HasAccess = allowed;
                IsSpecialThemeAllowed = specialThemeAllowed;
                IsAdmin = isAdmin;
                IsAdminDevice = isAdmin;
                return allowed;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ChatService.CheckAccessAsync failed.");
                HasAccess = false;
                IsSpecialThemeAllowed = false;
                IsAdmin = false;
                IsAdminDevice = false;
                return false;
            }
        }

        public async Task ConnectAsync(string nickname)
        {
            if (_disposed || IsConnected)
            {
                return;
            }

            if (!IntegrityChecker.ValidateAssemblyIntegrity())
            {
                return;
            }

            await DisconnectAsync();

            string hwid = HardwareFingerprintHelper.GetMachineHwid();
            string cleanNick = string.IsNullOrWhiteSpace(nickname) ? "Elf" : nickname.Trim();
            if (cleanNick.Length > 24)
            {
                cleanNick = cleanNick[..24];
            }

            string wsUrl =
                Constants
                    .GatekeeperWorkerUrl.Replace(
                        "https://",
                        "wss://",
                        StringComparison.OrdinalIgnoreCase
                    )
                    .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase) + "/chat";

            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("X-Api-Key", Constants.GatekeeperClientAppId);
            _webSocket.Options.SetRequestHeader("X-HWID", hwid);
            _webSocket.Options.SetRequestHeader("X-Nickname", cleanNick);
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(25);

            _receiveCts = new CancellationTokenSource();
            await _webSocket.ConnectAsync(new Uri(wsUrl), _receiveCts.Token);
            ConnectionStateChanged?.Invoke(this, true);

            _ = Task.Run(() => ReceiveLoopAsync(_webSocket, _receiveCts.Token));
        }

        public async Task DisconnectAsync()
        {
            if (_receiveCts != null)
            {
                await _receiveCts.CancelAsync();
                _receiveCts.Dispose();
                _receiveCts = null;
            }

            if (_webSocket != null)
            {
                try
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None
                        );
                    }
                }
                catch(Exception ex)
                {
                    Log.Error("Error while closing WebSocket connection.", ex);
                }
                finally
                {
                    _webSocket.Dispose();
                    _webSocket = null;
                    ConnectionStateChanged?.Invoke(this, false);
                }
            }
        }

        public async Task SendAsync(string text)
        {
            if (_webSocket?.State != WebSocketState.Open || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string payload = JsonSerializer.Serialize(new { text = text.Trim() });
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            await _sendLock.WaitAsync();
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None
                    );
                }
            }
            finally
            {
                _ = _sendLock.Release();
            }
        }

        public async Task<(
            bool Success,
            string? UnlockedSkinId,
            string? Error
        )> ClaimSkinVoucherAsync(string skinId, string skinName)
        {
            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                string url = $"{Constants.GatekeeperWorkerUrl}/chat/claim-skin";
                using HttpRequestMessage req = new(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            hwid,
                            skinId,
                            skinName,
                        }
                    ),
                };
                _ = req.Headers.TryAddWithoutValidation(
                    "X-Api-Key",
                    Constants.GatekeeperClientAppId
                );

                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                bool success =
                    root.TryGetProperty("success", out JsonElement sEl) && sEl.GetBoolean();
                string? unlockedId = root.TryGetProperty("unlockedSkinId", out JsonElement idEl)
                    ? idEl.GetString()
                    : null;
                string? error = root.TryGetProperty("error", out JsonElement errEl)
                    ? errEl.GetString()
                    : null;

                return (success, unlockedId, error);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] buffer = new byte[4096];
            using MemoryStream ms = new();

            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            ConnectionStateChanged?.Invoke(this, false);
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    string rawJson = Encoding.UTF8.GetString(ms.ToArray());
                    HandleServerPayload(rawJson);
                }
            }
            catch (OperationCanceledException ex)
            {
                Log.Error("ChatService receive loop canceled.", ex);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ChatService receive loop ended unexpectedly.");
            }
            finally
            {
                ConnectionStateChanged?.Invoke(this, false);
            }
        }

        private void HandleServerPayload(string rawJson)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(rawJson);
                JsonElement root = doc.RootElement;
                string type = root.TryGetProperty("type", out JsonElement typeEl)
                    ? typeEl.GetString() ?? ""
                    : "";

                switch (type)
                {
                    case "init":
                        EmitStats(root);
                        if (
                            root.TryGetProperty("history", out JsonElement histEl)
                            && histEl.ValueKind == JsonValueKind.Array
                        )
                        {
                            List<ChatMessage> list = [];
                            foreach (JsonElement item in histEl.EnumerateArray())
                            {
                                list.Add(ParseMessage(item));
                            }
                            HistoryReceived?.Invoke(this, list);
                        }
                        break;

                    case "chat":
                    case "slash_reply":
                        MessageReceived?.Invoke(this, ParseMessage(root));
                        EmitStats(root);
                        break;

                    case "stats":
                        EmitStats(root);
                        break;

                    case "skin_voucher_won":
                        SkinVoucherWon?.Invoke(this, EventArgs.Empty);
                        break;

                    case "grand_prize":
                        string key = root.TryGetProperty("key", out JsonElement kEl)
                            ? kEl.GetString() ?? ""
                            : "";
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            GrandPrizeReceived?.Invoke(this, key);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to parse chat server payload.");
            }
        }

        private void EmitStats(JsonElement root)
        {
            if (
                !root.TryGetProperty("snowflakes", out JsonElement sfEl)
                && !root.TryGetProperty("streak", out _)
            )
            {
                return;
            }

            int snowflakes = root.TryGetProperty("snowflakes", out sfEl) ? sfEl.GetInt32() : -1;
            int streak = root.TryGetProperty("streak", out JsonElement stEl) ? stEl.GetInt32() : -1;
            string badge = root.TryGetProperty("badge", out JsonElement bEl)
                ? bEl.GetString() ?? ""
                : "";

            StatsUpdated?.Invoke(this, (snowflakes, streak, badge));

            if (
                root.TryGetProperty("unlockedBadges", out JsonElement ubEl)
                && ubEl.ValueKind == JsonValueKind.Array
            )
            {
                List<string> badges = [];
                foreach (JsonElement b in ubEl.EnumerateArray())
                {
                    string? s = b.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        badges.Add(s);
                    }
                }
                UnlockedBadgesUpdated?.Invoke(this, badges);
            }
        }

        private static ChatMessage ParseMessage(JsonElement el)
        {
            long id = el.TryGetProperty("id", out JsonElement idEl) ? idEl.GetInt64() : 0;
            string nick = el.TryGetProperty("nickname", out JsonElement nEl)
                ? nEl.GetString() ?? "Elf"
                : "Elf";
            string badge = el.TryGetProperty("badge", out JsonElement bEl)
                ? bEl.GetString() ?? ""
                : "";
            string text = el.TryGetProperty("text", out JsonElement tEl)
                ? tEl.GetString() ?? ""
                : "";
            bool isSys = el.TryGetProperty("isSystem", out JsonElement sEl) && sEl.GetBoolean();
            bool isPriv =
                el.TryGetProperty("isPrivateReply", out JsonElement pEl) && pEl.GetBoolean();
            long ts = el.TryGetProperty("timestamp", out JsonElement tsEl)
                ? tsEl.GetInt64()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return new ChatMessage
            {
                Id = id,
                Nickname = nick,
                Badge = badge,
                Text = text,
                IsSystem = isSys,
                IsPrivateReply = isPriv,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ts),
            };
        }

        private static HttpRequestMessage CreateAdminRequest(
            HttpMethod method,
            string path,
            string adminSecret
        )
        {
            HttpRequestMessage req = new(method, $"{Constants.GatekeeperWorkerUrl}{path}");
            _ = req.Headers.TryAddWithoutValidation("X-Admin-Secret", adminSecret);
            _ = req.Headers.TryAddWithoutValidation(
                "X-Admin-Hwid",
                HardwareFingerprintHelper.GetMachineHwid()
            );
            return req;
        }

        public async Task<ChatAdminStatsResult> GetAdminStatsAsync(string adminSecret)
        {
            try
            {
                using HttpRequestMessage req = CreateAdminRequest(
                    HttpMethod.Get,
                    "/chat/admin/stats",
                    adminSecret
                );
                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (
                    !res.IsSuccessStatusCode
                    || !(root.TryGetProperty("success", out JsonElement sEl) && sEl.GetBoolean())
                )
                {
                    string err = root.TryGetProperty("error", out JsonElement eEl)
                        ? eEl.GetString() ?? $"HTTP {(int)res.StatusCode}"
                        : $"HTTP {(int)res.StatusCode}";
                    return new ChatAdminStatsResult { Success = false, Error = err };
                }

                ChatAdminStatsResult result = new()
                {
                    Success = true,
                    TotalTickets = root.TryGetProperty("totalTickets", out JsonElement ttEl)
                        ? ttEl.GetInt32()
                        : 0,
                    MessageCount = root.TryGetProperty("messageCount", out JsonElement mcEl)
                        ? mcEl.GetInt32()
                        : 0,
                    EventEnded =
                        root.TryGetProperty("eventEnded", out JsonElement eeEl)
                        && eeEl.GetBoolean(),
                    WinnerNickname = root.TryGetProperty("winnerNickname", out JsonElement wnEl)
                        ? wnEl.GetString() ?? string.Empty
                        : string.Empty,
                    WinnerKey = root.TryGetProperty("winnerKey", out JsonElement wkEl)
                        ? wkEl.GetString() ?? string.Empty
                        : string.Empty,
                };

                if (
                    root.TryGetProperty("participants", out JsonElement pEl)
                    && pEl.ValueKind == JsonValueKind.Array
                )
                {
                    foreach (JsonElement item in pEl.EnumerateArray())
                    {
                        result.Participants.Add(
                            new ChatParticipantItem
                            {
                                Hwid = item.TryGetProperty("hwid", out JsonElement h)
                                    ? h.GetString() ?? ""
                                    : "",
                                Nickname = item.TryGetProperty("nickname", out JsonElement n)
                                    ? n.GetString() ?? ""
                                    : "",
                                Snowflakes = item.TryGetProperty("snowflakes", out JsonElement sf)
                                    ? sf.GetInt32()
                                    : 0,
                                Streak = item.TryGetProperty("streak", out JsonElement st)
                                    ? st.GetInt32()
                                    : 0,
                                Badge = item.TryGetProperty("badge", out JsonElement b)
                                    ? b.GetString() ?? ""
                                    : "",
                                BadgeTier = item.TryGetProperty("badge_tier", out JsonElement bt)
                                    ? bt.GetString() ?? ""
                                    : "",
                                LastCheckinDate = item.TryGetProperty(
                                    "last_checkin_date",
                                    out JsonElement lc
                                )
                                    ? lc.GetString() ?? ""
                                    : "",
                            }
                        );
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ChatAdminStatsResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<(
            bool Success,
            string? WinnerNickname,
            int WinnerSnowflakes,
            string? Key,
            string? Error
        )> DrawRaffleWinnerAsync(string adminSecret)
        {
            try
            {
                using HttpRequestMessage req = CreateAdminRequest(
                    HttpMethod.Post,
                    "/chat/admin/draw-winner",
                    adminSecret
                );
                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (
                    !res.IsSuccessStatusCode
                    || !(root.TryGetProperty("success", out JsonElement sEl) && sEl.GetBoolean())
                )
                {
                    string err = root.TryGetProperty("error", out JsonElement eEl)
                        ? eEl.GetString() ?? $"HTTP {(int)res.StatusCode}"
                        : $"HTTP {(int)res.StatusCode}";
                    return (false, null, 0, null, err);
                }

                string? key = root.TryGetProperty("key", out JsonElement kEl)
                    ? kEl.GetString()
                    : null;
                string? winnerNick = null;
                int winnerSnowflakes = 0;
                if (root.TryGetProperty("winner", out JsonElement wEl))
                {
                    winnerNick = wEl.TryGetProperty("nickname", out JsonElement wn)
                        ? wn.GetString()
                        : null;
                    winnerSnowflakes = wEl.TryGetProperty("snowflakes", out JsonElement ws)
                        ? ws.GetInt32()
                        : 0;
                }

                return (true, winnerNick, winnerSnowflakes, key, null);
            }
            catch (Exception ex)
            {
                return (false, null, 0, null, ex.Message);
            }
        }

        public async Task<(bool Success, string? Message)> ResetChatEventAsync(string adminSecret)
        {
            try
            {
                using HttpRequestMessage req = CreateAdminRequest(
                    HttpMethod.Post,
                    "/chat/admin/reset",
                    adminSecret
                );
                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                bool success =
                    res.IsSuccessStatusCode
                    && root.TryGetProperty("success", out JsonElement sEl)
                    && sEl.GetBoolean();
                string? msg = root.TryGetProperty("message", out JsonElement mEl)
                    ? mEl.GetString()
                    : (
                        root.TryGetProperty("error", out JsonElement eEl)
                            ? eEl.GetString()
                            : $"HTTP {(int)res.StatusCode}"
                    );
                return (success, msg);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, int NewSnowflakes, string? Error)> AdjustSnowflakesAsync(
            string adminSecret,
            string hwid,
            int delta
        )
        {
            try
            {
                using HttpRequestMessage req = CreateAdminRequest(
                    HttpMethod.Post,
                    "/chat/admin/adjust-snowflakes",
                    adminSecret
                );
                req.Content = JsonContent.Create(new { hwid, delta });
                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                bool success =
                    res.IsSuccessStatusCode
                    && root.TryGetProperty("success", out JsonElement sEl)
                    && sEl.GetBoolean();
                int newBal = root.TryGetProperty("snowflakes", out JsonElement sfEl)
                    ? sfEl.GetInt32()
                    : 0;
                string? err = root.TryGetProperty("error", out JsonElement eEl)
                    ? eEl.GetString()
                    : (success ? null : $"HTTP {(int)res.StatusCode}");
                return (success, newBal, err);
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message);
            }
        }

        public async Task<(bool Success, string? Message)> GrantAdminRewardAsync(
            string adminSecret,
            string hwid,
            string rewardType,
            string value,
            string? nickname = null
        )
        {
            try
            {
                using HttpRequestMessage req = CreateAdminRequest(
                    HttpMethod.Post,
                    "/chat/admin/grant-reward",
                    adminSecret
                );
                req.Content = JsonContent.Create(
                    new
                    {
                        hwid,
                        rewardType,
                        value,
                        nickname = nickname ?? "Player",
                    }
                );
                using HttpResponseMessage res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                bool success =
                    res.IsSuccessStatusCode
                    && root.TryGetProperty("success", out JsonElement sEl)
                    && sEl.GetBoolean();
                string? msg = root.TryGetProperty("message", out JsonElement mEl)
                    ? mEl.GetString()
                    : (
                        root.TryGetProperty("error", out JsonElement eEl)
                            ? eEl.GetString()
                            : (success ? "Reward granted." : $"HTTP {(int)res.StatusCode}")
                    );
                return (success, msg);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _webSocket?.Dispose();
            _sendLock.Dispose();
        }
    }
}
