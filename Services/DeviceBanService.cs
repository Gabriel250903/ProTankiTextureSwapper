using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public class DeviceBanService : IDeviceBanService, IDisposable
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        private string? _cachedHwid;
        private CancellationTokenSource? _heartbeatCts;
        private bool _isPeriodicRunning;
        private bool _isCurrentlyBanned;

        private List<AdminDeviceItem>? _cachedDevices;
        private DateTime _devicesCacheExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _devicesCacheLock = new(1, 1);

        private DeviceBanCheckResult? _lastCheckResult;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private static readonly TimeSpan ClientCacheDuration = TimeSpan.FromMinutes(10);

        public event EventHandler<DeviceBanCheckResult>? DeviceBanned;
        public event EventHandler? DeviceUnbanned;

        public string GetDeviceHardwareId()
        {
            _cachedHwid ??= HardwareFingerprintHelper.GetMachineHwid();
            return _cachedHwid;
        }

        public void StartPeriodicVerification(TimeSpan interval)
        {
            if (_isPeriodicRunning)
            {
                return;
            }

            _isPeriodicRunning = true;
            _heartbeatCts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                using PeriodicTimer timer = new(interval);
                while (_heartbeatCts != null && !_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (await timer.WaitForNextTickAsync(_heartbeatCts.Token))
                        {
                            DeviceBanCheckResult result = await VerifyDeviceStatusAsync();

                            if (result.IsBanned)
                            {
                                if (!_isCurrentlyBanned)
                                {
                                    _isCurrentlyBanned = true;
                                    Log.Warning(
                                        "Periodic Heartbeat: Device ban detected in real-time. Firing DeviceBanned event."
                                    );
                                    DeviceBanned?.Invoke(this, result);
                                }
                            }
                            else if (!result.NetworkError)
                            {
                                if (_isCurrentlyBanned)
                                {
                                    _isCurrentlyBanned = false;
                                    Log.Information(
                                        "Periodic Heartbeat: Device ban has been lifted in real-time. Firing DeviceUnbanned event."
                                    );
                                    DeviceUnbanned?.Invoke(this, EventArgs.Empty);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(
                            ex,
                            "Periodic Heartbeat: Error during background device verification."
                        );
                    }
                }
            });
        }

        public void StopPeriodicVerification()
        {
            _isPeriodicRunning = false;
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
        }

        public void Dispose()
        {
            StopPeriodicVerification();
        }

        public async Task<DeviceBanCheckResult> VerifyDeviceStatusAsync(bool forceRefresh = false)
        {
            if (
                !forceRefresh
                && _lastCheckResult != null
                && (DateTime.UtcNow - _lastCheckTime) < ClientCacheDuration
            )
            {
                return _lastCheckResult;
            }

            DeviceBanCheckResult result = new();

            try
            {
                string hwid = GetDeviceHardwareId();
                Log.Verbose("DeviceBanService: Verifying device status for HWID: {Hwid}", hwid);

                string requestUrl =
                    $"{Constants.GatekeeperWorkerUrl}/check/{Uri.EscapeDataString(hwid)}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                using HttpRequestMessage request = new(HttpMethod.Get, requestUrl);
                request.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                };

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    result.IsBanned = true;
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("reason", out JsonElement reasonElement))
                        {
                            string? reason = reasonElement.GetString();
                            if (!string.IsNullOrWhiteSpace(reason))
                            {
                                result.Reason = reason;
                            }
                        }

                        if (doc.RootElement.TryGetProperty("isTemporary", out JsonElement tempEl))
                        {
                            result.IsTemporary = tempEl.GetBoolean();
                        }
                        else if (
                            doc.RootElement.TryGetProperty("is_temp_ban", out JsonElement tempEl2)
                        )
                        {
                            result.IsTemporary = tempEl2.GetBoolean();
                        }

                        if (doc.RootElement.TryGetProperty("expiresAt", out JsonElement expEl))
                        {
                            result.ExpiresAt = expEl.GetString() ?? string.Empty;
                        }
                        else if (
                            doc.RootElement.TryGetProperty("ban_expires_at", out JsonElement expEl2)
                        )
                        {
                            result.ExpiresAt = expEl2.GetString() ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "DeviceBanService: Failed to parse ban information.");
                    }

                    if (string.IsNullOrWhiteSpace(result.Reason))
                    {
                        result.Reason = result.IsTemporary
                            ? "This device has been temporarily suspended from TextureSwapper."
                            : "This device has been permanently suspended from TextureSwapper.";
                    }

                    _lastCheckResult = result;
                    _lastCheckTime = DateTime.UtcNow;

                    Log.Warning(
                        "DeviceBanService: Device is BANNED. Temp: {IsTemp}, Reason: {Reason}, Expires: {ExpiresAt}",
                        result.IsTemporary,
                        result.Reason,
                        result.ExpiresAt
                    );
                    return result;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Log.Error("DeviceBanService: API key rejected by Cloudflare Worker.");
                    result.NetworkError = true;
                    return result;
                }

                if (response.IsSuccessStatusCode)
                {
                    result.IsBanned = false;
                    _lastCheckResult = result;
                    _lastCheckTime = DateTime.UtcNow;
                    Log.Verbose("DeviceBanService: Device is authorized.");
                    return result;
                }

                Log.Warning(
                    "DeviceBanService: Unexpected response status: {StatusCode}",
                    response.StatusCode
                );
                result.NetworkError = true;
                return result;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("DeviceBanService: Request timed out.");
                result.NetworkError = true;
                return result;
            }
            catch (HttpRequestException ex)
            {
                Log.Warning(ex, "DeviceBanService: Network error during device verification.");
                result.NetworkError = true;
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceBanService: Unexpected error during device verification.");
                result.NetworkError = true;
                return result;
            }
        }

        public async Task<List<AdminDeviceItem>> GetAllDevicesAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedDevices != null && DateTime.UtcNow < _devicesCacheExpiry)
            {
                return _cachedDevices;
            }

            await _devicesCacheLock.WaitAsync();
            try
            {
                if (!AdminSession.IsAuthenticated)
                {
                    Log.Warning(
                        "DeviceBanService: Cannot fetch devices without active admin session."
                    );
                    return _cachedDevices ?? [];
                }

                if (
                    !forceRefresh
                    && _cachedDevices != null
                    && DateTime.UtcNow < _devicesCacheExpiry
                )
                {
                    return _cachedDevices;
                }

                string requestUrl =
                    $"{Constants.GatekeeperWorkerUrl}/admin/devices?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                using HttpRequestMessage request = new(HttpMethod.Get, requestUrl);
                request.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                request.Headers.Add("X-Admin-Secret", AdminSession.CurrentSecret ?? string.Empty);
                request.Headers.Add("X-Admin-Hwid", GetDeviceHardwareId());
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                };

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning(
                        "DeviceBanService: Failed to fetch devices. Status: {StatusCode}",
                        response.StatusCode
                    );
                    return _cachedDevices ?? [];
                }

                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("devices", out JsonElement devicesEl))
                {
                    List<AdminDeviceItem>? list = JsonSerializer.Deserialize<List<AdminDeviceItem>>(
                        devicesEl.GetRawText()
                    );
                    if (list != null)
                    {
                        _cachedDevices = list;
                        _devicesCacheExpiry = DateTime.UtcNow.AddSeconds(20);
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceBanService: Error fetching device list.");
            }
            finally
            {
                _ = _devicesCacheLock.Release();
            }

            return _cachedDevices ?? [];
        }

        public async Task<bool> BanDeviceAsync(string hwid, string reason, int durationHours = 0)
        {
            if (!AdminSession.IsAuthenticated)
            {
                Log.Warning("DeviceBanService: Cannot ban device without active admin session.");
                return false;
            }

            try
            {
                _cachedDevices = null;
                _devicesCacheExpiry = DateTime.MinValue;
                _lastCheckResult = null;
                _lastCheckTime = DateTime.MinValue;

                string requestUrl = $"{Constants.GatekeeperWorkerUrl}/admin/ban";
                Dictionary<string, object> payload = new()
                {
                    ["hwid"] = hwid.Trim(),
                    ["reason"] = string.IsNullOrWhiteSpace(reason)
                        ? "Banned by administrator."
                        : reason.Trim(),
                    ["duration_hours"] = durationHours,
                };
                string jsonPayload = JsonSerializer.Serialize(payload);

                using HttpRequestMessage request = new(HttpMethod.Post, requestUrl);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                request.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                request.Headers.Add("X-Admin-Secret", AdminSession.CurrentSecret ?? string.Empty);
                request.Headers.Add("X-Admin-Hwid", GetDeviceHardwareId());

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceBanService: Error banning device {Hwid}", hwid);
                return false;
            }
        }

        public async Task<bool> UnbanDeviceAsync(string hwid)
        {
            if (!AdminSession.IsAuthenticated)
            {
                Log.Warning("DeviceBanService: Cannot unban device without active admin session.");
                return false;
            }

            try
            {
                _cachedDevices = null;
                _devicesCacheExpiry = DateTime.MinValue;
                _lastCheckResult = null;
                _lastCheckTime = DateTime.MinValue;

                string requestUrl = $"{Constants.GatekeeperWorkerUrl}/admin/unban";
                Dictionary<string, string> payload = new()
                {
                    ["hwid"] = hwid.Trim(),
                };
                string jsonPayload = JsonSerializer.Serialize(payload);

                using HttpRequestMessage request = new(HttpMethod.Post, requestUrl);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                request.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                request.Headers.Add("X-Admin-Secret", AdminSession.CurrentSecret ?? string.Empty);
                request.Headers.Add("X-Admin-Hwid", GetDeviceHardwareId());

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceBanService: Error unbanning device {Hwid}", hwid);
                return false;
            }
        }

        public async Task<(bool Success, string Message)> AuthenticateAdminAsync(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password cannot be empty.");
            }

            try
            {
                string hwid = GetDeviceHardwareId();
                string requestUrl = $"{Constants.GatekeeperWorkerUrl}/admin/auth";
                Dictionary<string, string> payload = new()
                {
                    ["secret"] = password.Trim(),
                    ["hwid"] = hwid,
                };
                string jsonPayload = JsonSerializer.Serialize(payload);

                using HttpRequestMessage request = new(HttpMethod.Post, requestUrl);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                request.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                request.Headers.Add("X-Admin-Secret", password.Trim());
                request.Headers.Add("X-Admin-Hwid", hwid);

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Authenticated successfully.");
                }

                string errorMessage = "Invalid admin password or unauthorized device.";
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out JsonElement errEl))
                    {
                        errorMessage = errEl.GetString() ?? errorMessage;
                    }
                }
                catch { }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return (false, errorMessage);
                }

                return (
                    false,
                    $"Authentication failed ({(int)response.StatusCode}): {errorMessage}"
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceBanService: Error during admin authentication.");
                return (false, $"Connection error: {ex.Message}");
            }
        }
    }
}
