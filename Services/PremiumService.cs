using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed partial class PremiumService : IPremiumService
    {
        internal const string PUBLIC_KEY_PEM = """
            -----BEGIN PUBLIC KEY-----
            MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxmaRfFzg5nGKbVcQ9ozE
            TEwFSX/oHrhEttYxLIZzdhgBNMEtGJk5cHbBSXoBZ/56woh1rV18Q+wWo7e8DKZh
            ayunAzzyB5hgLJNQ2ZhMl5WdkNAKTz4N0Lr3D4TMpFcQhvXT3fwuv/02LcK4UVq7
            yRUqHt8CFzQCjay44v1SZRqN5hLFiO24tmt5z4xP7oZH8c1AstJl3gm7lKajfuaY
            ve06mh281GVaj4tfbWn2lz0eN2JRC4ABH00EgULUMU6zrDR+t4wBhwypOlYAE24C
            b2eEHfha4+IFXV8MOxPXupV6itPKP7G91A0F1n5dWe7cHjbtKrSjsuLoZm2V8Blt
            oQIDAQAB
            -----END PUBLIC KEY-----
            """;

        private const string ACTIVATION_URL =
            "https://textureswapper-gatekeeper.flytoxp.workers.dev/license";

        private static HttpClient CreateDefaultHttpClient()
        {
            HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TextureSwapper/2.0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", Constants.GatekeeperApiKey);
            return client;
        }

        private static readonly HttpClient _defaultHttpClient = CreateDefaultHttpClient();
        private readonly HttpClient _httpClient;

        internal static readonly TimeSpan TrialOfflineGracePeriod = TimeSpan.FromHours(24);
        internal static readonly TimeSpan MaxOfflineGracePeriod = TimeSpan.FromDays(5);
        internal static readonly TimeSpan OnlineRecheckInterval = TimeSpan.FromHours(12);
        internal static readonly TimeSpan ClockDriftTolerance = TimeSpan.FromMinutes(5);

        public event EventHandler<bool>? PremiumStatusChanged;
        private readonly ISettingsService _settingsService;
        private AppSettings? _lastSettings;

        public PremiumService(ISettingsService settingsService)
            : this(settingsService, _defaultHttpClient) { }

        internal PremiumService(ISettingsService settingsService, HttpClient httpClient)
        {
            _settingsService = settingsService;
            _httpClient = httpClient;
            try
            {
                _lastSettings = _settingsService.Load();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load initial Settings in PremiumService.");
            }
        }

        private List<ServerLicenseKeyItem>? _cachedKeys;
        private DateTime _keysCacheExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _keysCacheLock = new(1, 1);

        private byte[]? _cachedAssetKey;
        private readonly SemaphoreSlim _assetKeyLock = new(1, 1);

        public bool IsPremiumUnlocked
        {
            get
            {
                if (_lastSettings == null || !_lastSettings.IsPremiumUnlocked)
                {
                    return false;
                }

                if (_lastSettings.IsTrialLicense)
                {
                    if (TryParseIso8601(_lastSettings.PremiumExpiresAtUtc, out DateTime expiresAt))
                    {
                        if (DateTime.UtcNow >= expiresAt)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool IsTrialActive =>
            _lastSettings != null
            && _lastSettings.IsTrialLicense
            && IsPremiumUnlocked
            && TryParseIso8601(_lastSettings.PremiumExpiresAtUtc, out DateTime expiresAt)
            && DateTime.UtcNow < expiresAt;

        public TimeSpan? TrialTimeRemaining
        {
            get
            {
                if (
                    _lastSettings == null
                    || (!_lastSettings.IsTrialLicense && !_lastSettings.HasClaimedTrial)
                )
                {
                    return null;
                }

                if (TryParseIso8601(_lastSettings.PremiumExpiresAtUtc, out DateTime expiresAt))
                {
                    TimeSpan remaining = expiresAt - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }

                return _lastSettings.HasClaimedTrial ? TimeSpan.Zero : null;
            }
        }

        private async Task<HttpResponseMessage> PostAdminJsonAsync<T>(string url, T payload)
        {
            if (!AdminSession.IsAuthenticated)
            {
                Log.Warning(
                    "PremiumService: PostAdminJsonAsync called without active admin session."
                );
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            HttpRequestMessage req = new(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload),
            };
            req.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
            req.Headers.Add("X-Admin-Secret", AdminSession.CurrentSecret ?? string.Empty);
            req.Headers.Add("X-Admin-Hwid", HardwareFingerprintHelper.GetMachineHwid());
            return await _httpClient.SendAsync(req);
        }

        public async Task<bool> RevokeLicenseKeyAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                string targetKey = licenseKey.Trim();
                Dictionary<string, string> payload = new()
                {
                    { "licenseKey", targetKey },
                    { "action", "revoke" },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                if (
                    _lastSettings != null
                    && targetKey.Equals(
                        _lastSettings.PremiumLicenseKey,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    ClearPremiumState(_lastSettings);
                    PremiumStatusChanged?.Invoke(this, false);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to revoke license key via Admin panel.");
                return false;
            }
        }

        public async Task<bool> UnbindHwidAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                string targetKey = licenseKey.Trim();
                Dictionary<string, string> payload = new()
                {
                    { "licenseKey", targetKey },
                    { "action", "unbind" },
                };
                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                if (
                    response.IsSuccessStatusCode
                    && _lastSettings != null
                    && targetKey.Equals(
                        _lastSettings.PremiumLicenseKey,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    ClearPremiumState(_lastSettings);
                    PremiumStatusChanged?.Invoke(this, false);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to unbind HWID via Admin panel.");
                return false;
            }
        }

        public async Task<bool> CreateLicenseKeyAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "licenseKey", licenseKey.Trim() },
                    { "action", "create" },
                };
                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create new license key via Admin panel.");
                return false;
            }
        }

        public async Task<bool> DeleteLicenseKeyAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                string targetKey = licenseKey.Trim();
                Dictionary<string, string> payload = new()
                {
                    { "licenseKey", targetKey },
                    { "action", "delete" },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                if (
                    _lastSettings != null
                    && targetKey.Equals(
                        _lastSettings.PremiumLicenseKey,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    ClearPremiumState(_lastSettings);
                    _lastSettings.PremiumLicenseKey = string.Empty;
                    PremiumStatusChanged?.Invoke(this, false);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete license key via Admin panel.");
                return false;
            }
        }

        public async Task<PremiumActivationResult> AssignKeyToDeviceAsync(
            string licenseKey,
            string targetHwid,
            string nickname
        )
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "License key cannot be empty.",
                };
            }

            if (string.IsNullOrWhiteSpace(targetHwid))
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "Target device HWID cannot be empty.",
                };
            }

            string cleanNickname = string.IsNullOrWhiteSpace(nickname) ? "User" : nickname.Trim();

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "action", "assign" },
                    { "licenseKey", licenseKey.Trim() },
                    { "hwid", targetHwid.Trim() },
                    { "nickname", cleanNickname },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                ServerActivationResponse? result = null;
                try
                {
                    result = await response.Content.ReadFromJsonAsync<ServerActivationResponse>();
                }
                catch (Exception jsonEx)
                {
                    Log.Warning(jsonEx, "Could not parse JSON from server response.");
                }

                if (
                    result != null
                    && !result.Success
                    && result.Error != null
                    && result.Error.Contains(
                        "already registered",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    Log.Information(
                        "Key is bound to another device. Unbinding and re-assigning..."
                    );
                    _ = await UnbindHwidAsync(licenseKey.Trim());
                    using HttpResponseMessage retryResponse = await PostAdminJsonAsync(
                        ACTIVATION_URL,
                        payload
                    );
                    try
                    {
                        result =
                            await retryResponse.Content.ReadFromJsonAsync<ServerActivationResponse>();
                    }
                    catch (Exception jsonEx)
                    {
                        Log.Warning(jsonEx, "Could not parse JSON from retry response.");
                    }
                }

                if (!response.IsSuccessStatusCode && (result == null || !result.Success))
                {
                    string errorMsg =
                        result?.Error
                        ?? result?.Message
                        ?? $"Server error (HTTP {(int)response.StatusCode}).";
                    return new PremiumActivationResult { Success = false, ErrorMessage = errorMsg };
                }

                if (result == null || !result.Success)
                {
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = result?.Error ?? "Failed to assign key on server.",
                    };
                }

                string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                if (targetHwid.Trim().Equals(currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    _lastSettings ??= _settingsService.Load();
                    _lastSettings.PremiumLicenseKey = licenseKey.Trim();
                    _lastSettings.UserNickname = cleanNickname;
                    _lastSettings.IsPremiumUnlocked = true;
                    _lastSettings.IsTrialLicense = false;
                    if (!string.IsNullOrEmpty(result.Signature))
                    {
                        _lastSettings.PremiumActivationSignature = result.Signature;
                    }
                    if (!string.IsNullOrEmpty(result.ExpiresAt))
                    {
                        _lastSettings.PremiumExpiresAtUtc = result.ExpiresAt;
                    }
                    _lastSettings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                    _settingsService.Save(_lastSettings);
                    PremiumStatusChanged?.Invoke(this, true);
                }

                return new PremiumActivationResult
                {
                    Success = true,
                    Signature = result.Signature ?? string.Empty,
                    ExpiresAt = result.ExpiresAt ?? string.Empty,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error assigning key to device.");
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = $"Assignment failed: {ex.Message}",
                };
            }
        }

        public async Task<(bool Success, string Message)> UpdateNicknameAsync(
            string newNickname,
            string? licenseKey = null,
            string? hwid = null
        )
        {
            if (string.IsNullOrWhiteSpace(newNickname))
            {
                return (false, "Nickname cannot be empty.");
            }

            string cleanNickname = newNickname.Trim();
            string cleanKey = licenseKey?.Trim() ?? string.Empty;
            string cleanHwid = hwid?.Trim() ?? string.Empty;

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "action", "update_nickname" },
                    { "nickname", cleanNickname },
                    { "licenseKey", cleanKey },
                    { "hwid", cleanHwid },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                if (response.IsSuccessStatusCode)
                {
                    string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                    if (!string.IsNullOrEmpty(cleanHwid) && cleanHwid.Equals(currentHwid, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastSettings ??= _settingsService.Load();
                        _lastSettings.UserNickname = cleanNickname;
                        _settingsService.Save(_lastSettings);
                    }
                    return (true, $"Nickname updated to '{cleanNickname}'.");
                }

                if (!string.IsNullOrEmpty(cleanKey) && !string.IsNullOrEmpty(cleanHwid))
                {
                    PremiumActivationResult fallbackRes = await AssignKeyToDeviceAsync(cleanKey, cleanHwid, cleanNickname);
                    if (fallbackRes.Success)
                    {
                        return (true, $"Nickname updated to '{cleanNickname}'.");
                    }
                }

                string errBody = await response.Content.ReadAsStringAsync();
                return (false, $"Server returned error: {response.StatusCode} {errBody}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating user nickname.");
                return (false, ex.Message);
            }
        }

        public async Task<List<ServerLicenseKeyItem>> FetchAllLicenseKeysAsync(
            bool forceRefresh = false
        )
        {
            if (!forceRefresh && _cachedKeys != null && DateTime.UtcNow < _keysCacheExpiry)
            {
                return _cachedKeys;
            }

            await _keysCacheLock.WaitAsync();
            try
            {
                if (!AdminSession.IsAuthenticated)
                {
                    Log.Warning(
                        "PremiumService: FetchAllLicenseKeysAsync called without active admin session."
                    );
                    return _cachedKeys ?? [];
                }

                if (!forceRefresh && _cachedKeys != null && DateTime.UtcNow < _keysCacheExpiry)
                {
                    return _cachedKeys;
                }

                Dictionary<string, string> payload = new() { { "action", "list" } };

                using HttpRequestMessage req = new(HttpMethod.Post, ACTIVATION_URL)
                {
                    Content = JsonContent.Create(payload),
                };
                req.Headers.Add("X-Api-Key", Constants.GatekeeperApiKey);
                req.Headers.Add("X-Admin-Secret", AdminSession.CurrentSecret ?? string.Empty);
                req.Headers.Add("X-Admin-Hwid", HardwareFingerprintHelper.GetMachineHwid());
                req.Headers.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                };

                using HttpResponseMessage response = await _httpClient.SendAsync(req);
                if (response.IsSuccessStatusCode)
                {
                    ServerKeysListResponse? result =
                        await response.Content.ReadFromJsonAsync<ServerKeysListResponse>();
                    if (result?.Keys != null)
                    {
                        _cachedKeys = result.Keys;
                        _keysCacheExpiry = DateTime.UtcNow.AddSeconds(20);
                        return result.Keys;
                    }
                }
                else
                {
                    Log.Warning(
                        "FetchAllLicenseKeysAsync failed with HTTP {StatusCode}",
                        response.StatusCode
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch license keys list from server.");
            }
            finally
            {
                _ = _keysCacheLock.Release();
            }

            return _cachedKeys ?? [];
        }

        public async Task<(bool Success, string Message)> ResetDeviceTrialAsync(string hwid)
        {
            if (string.IsNullOrWhiteSpace(hwid))
            {
                return (false, "Device HWID is required.");
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "action", "admin_reset_trial" },
                    { "hwid", hwid.Trim() },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string err = ExtractServerError(
                        body,
                        $"Server returned HTTP {(int)response.StatusCode}"
                    );
                    return (false, err);
                }

                string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                if (hwid.Trim().Equals(currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastSettings != null && _lastSettings.IsTrialLicense)
                    {
                        ClearPremiumState(_lastSettings);
                        _lastSettings.HasClaimedTrial = false;
                        _settingsService.Save(_lastSettings);
                        PremiumStatusChanged?.Invoke(this, false);
                    }
                }

                return (true, "Trial tombstone reset successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reset device trial via Admin panel.");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> RevokeDeviceTrialAsync(string hwid)
        {
            if (string.IsNullOrWhiteSpace(hwid))
            {
                return (false, "Device HWID is required.");
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "action", "admin_revoke_trial" },
                    { "hwid", hwid.Trim() },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string err = ExtractServerError(
                        body,
                        $"Server returned HTTP {(int)response.StatusCode}"
                    );
                    return (false, err);
                }

                string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                if (hwid.Trim().Equals(currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    if (_lastSettings != null && _lastSettings.IsTrialLicense)
                    {
                        ClearPremiumState(_lastSettings);
                        _settingsService.Save(_lastSettings);
                        PremiumStatusChanged?.Invoke(this, false);
                    }
                }

                return (true, "Trial revoked successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to revoke device trial via Admin panel.");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> ExtendDeviceTrialAsync(
            string hwid,
            int days = 3,
            string nickname = ""
        )
        {
            if (string.IsNullOrWhiteSpace(hwid))
            {
                return (false, "Device HWID is required.");
            }

            try
            {
                _cachedKeys = null;
                _keysCacheExpiry = DateTime.MinValue;

                Dictionary<string, string> payload = new()
                {
                    { "action", "admin_activate_trial" },
                    { "hwid", hwid.Trim() },
                    { "days", days.ToString() },
                    { "nickname", string.IsNullOrWhiteSpace(nickname) ? "User" : nickname.Trim() },
                };

                using HttpResponseMessage response = await PostAdminJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string err = ExtractServerError(
                        body,
                        $"Server returned HTTP {(int)response.StatusCode}"
                    );
                    return (false, err);
                }

                string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                if (hwid.Trim().Equals(currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ServerActivationResponse? res =
                            JsonSerializer.Deserialize<ServerActivationResponse>(body);
                        if (res != null && res.Success)
                        {
                            _lastSettings ??= _settingsService.Load();
                            _lastSettings.PremiumLicenseKey = res.LicenseKey ?? string.Empty;
                            _lastSettings.UserNickname = string.IsNullOrWhiteSpace(nickname)
                                ? "User"
                                : nickname.Trim();
                            _lastSettings.IsPremiumUnlocked = true;
                            _lastSettings.IsTrialLicense = true;
                            _lastSettings.HasClaimedTrial = true;
                            if (!string.IsNullOrEmpty(res.Signature))
                            {
                                _lastSettings.PremiumActivationSignature = res.Signature;
                            }
                            if (!string.IsNullOrEmpty(res.ExpiresAt))
                            {
                                _lastSettings.PremiumExpiresAtUtc = res.ExpiresAt;
                            }
                            _lastSettings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                            _settingsService.Save(_lastSettings);
                            PremiumStatusChanged?.Invoke(this, true);
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Log.Warning(
                            parseEx,
                            "Could not parse activation response during trial extend."
                        );
                    }
                }
                return (true, $"Trial active for {days} days.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extend/activate device trial via Admin panel.");
                return (false, ex.Message);
            }
        }

        private static string ExtractServerError(string jsonBody, string fallback)
        {
            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return fallback;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonBody);
                if (doc.RootElement.TryGetProperty("error", out JsonElement errEl))
                {
                    string? errStr = errEl.GetString();
                    if (!string.IsNullOrWhiteSpace(errStr))
                    {
                        return errStr;
                    }
                }
                if (doc.RootElement.TryGetProperty("message", out JsonElement msgEl))
                {
                    string? msgStr = msgEl.GetString();
                    if (!string.IsNullOrWhiteSpace(msgStr))
                    {
                        return msgStr;
                    }
                }
            }
            catch { }
            return fallback;
        }

        private int _isSyncingAssignedLicense;

        public async Task<bool> CheckAndSyncAssignedLicenseAsync(
            AppSettings? settings = null,
            bool forceSync = false
        )
        {
            settings ??= _lastSettings ?? _settingsService.Load() ?? new AppSettings();

            if (Interlocked.CompareExchange(ref _isSyncingAssignedLicense, 1, 0) != 0)
            {
                return settings.IsPremiumUnlocked;
            }

            try
            {
                if (
                    !forceSync
                    && !settings.IsPremiumUnlocked
                    && string.IsNullOrWhiteSpace(settings.PremiumLicenseKey)
                    && settings.HasClaimedTrial
                )
                {
                    return false;
                }

                if (
                    settings.IsPremiumUnlocked
                    && !string.IsNullOrEmpty(settings.PremiumActivationSignature)
                )
                {
                    if (
                        TryParseIso8601(settings.LastVerifiedUtc, out DateTime lastOnline)
                        && (DateTime.UtcNow - lastOnline) < OnlineRecheckInterval
                    )
                    {
                        return true;
                    }
                }

                string myHwid = HardwareFingerprintHelper.GetMachineHwid();
                bool wasUnlocked = settings.IsPremiumUnlocked;

                Dictionary<string, string> payload = new()
                {
                    { "action", "sync_device" },
                    { "hwid", myHwid },
                    { "nickname", settings.UserNickname ?? string.Empty },
                };

                using HttpRequestMessage req = new(HttpMethod.Post, ACTIVATION_URL)
                {
                    Content = JsonContent.Create(payload),
                };
                if (!_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
                {
                    _ = req.Headers.TryAddWithoutValidation(
                        "X-Api-Key",
                        Constants.GatekeeperApiKey
                    );
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(req);

                if (response.IsSuccessStatusCode)
                {
                    string respJson = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(respJson);
                    JsonElement root = doc.RootElement;

                    bool hasLicense =
                        root.TryGetProperty("hasLicense", out JsonElement hl) && hl.GetBoolean();
                    string? key = root.TryGetProperty("licenseKey", out JsonElement lk)
                        ? lk.GetString()
                        : (root.TryGetProperty("key", out JsonElement k) ? k.GetString() : null);
                    string? sig = root.TryGetProperty("signature", out JsonElement sg)
                        ? sg.GetString()
                        : null;
                    string? expiresAt = root.TryGetProperty("expiresAt", out JsonElement exp)
                        ? exp.GetString()
                        : null;
                    string? nickname = root.TryGetProperty("nickname", out JsonElement nick)
                        ? nick.GetString()
                        : null;
                    bool isTrial =
                        (root.TryGetProperty("isTrial", out JsonElement it) && it.GetBoolean())
                        || (key?.StartsWith("TRIAL-", StringComparison.OrdinalIgnoreCase) ?? false);
                    bool expired =
                        root.TryGetProperty("expired", out JsonElement expElem)
                        && expElem.GetBoolean();
                    bool active =
                        !root.TryGetProperty("active", out JsonElement actElem)
                        || actElem.GetBoolean();

                    if (hasLicense && !string.IsNullOrEmpty(sig))
                    {
                        string targetExpiresAt = expiresAt ?? string.Empty;
                        string expectedPayload = $"{myHwid}:PREMIUM_UNLOCKED:{targetExpiresAt}";

                        if (!VerifyRsaSignature(expectedPayload, sig))
                        {
                            Log.Warning(
                                "License Sync: RSA signature validation failed for server response."
                            );
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }
                            return false;
                        }

                        bool isHalloweenPermanent =
                            (key?.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase) ?? false)
                            || (settings.PremiumLicenseKey?.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase) ?? false);

                        if (
                            !isHalloweenPermanent
                            && !string.IsNullOrEmpty(targetExpiresAt)
                            && TryParseIso8601(targetExpiresAt, out DateTime expDate)
                            && DateTime.UtcNow >= expDate
                        )
                        {
                            Log.Information(
                                "License Sync: Synced license has already expired ({ExpiresAt}). Locking premium.",
                                targetExpiresAt
                            );
                            if (isTrial)
                            {
                                settings.HasClaimedTrial = true;
                            }
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }
                            return false;
                        }

                        bool keyChanged = !string.Equals(
                            settings.PremiumLicenseKey,
                            key,
                            StringComparison.OrdinalIgnoreCase
                        );

                        settings.PremiumLicenseKey = key ?? settings.PremiumLicenseKey ?? string.Empty;
                        if (
                            !string.IsNullOrWhiteSpace(nickname)
                            && !string.Equals(
                                nickname,
                                "Supporter",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            settings.UserNickname = nickname;
                        }
                        else if (string.IsNullOrWhiteSpace(settings.UserNickname))
                        {
                            settings.UserNickname = !string.IsNullOrWhiteSpace(nickname)
                                ? nickname
                                : Environment.UserName;
                        }
                        settings.PremiumActivationSignature = sig;
                        settings.PremiumExpiresAtUtc = targetExpiresAt;
                        settings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                        settings.IsPremiumUnlocked = true;
                        settings.IsTrialLicense = isTrial;
                        if (isTrial)
                        {
                            settings.HasClaimedTrial = true;
                        }

                        _lastSettings = settings;
                        _settingsService.Save(settings);

                        if (!wasUnlocked || keyChanged)
                        {
                            Log.Information(
                                "License Sync: Successfully synced active {Type} license '{Key}' for device ({Hwid}).",
                                isTrial ? "Trial" : "Full",
                                settings.PremiumLicenseKey,
                                myHwid
                            );
                            PremiumStatusChanged?.Invoke(this, true);
                        }

                        return true;
                    }
                    else if (!hasLicense)
                    {
                        if (isTrial || expired || !active)
                        {
                            Log.Information(
                                "License Sync: Device recognized as having an expired trial. Updating local tombstone."
                            );
                            settings.HasClaimedTrial = true;
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }
                            return false;
                        }

                        if (settings.IsTrialLicense)
                        {
                            Log.Information(
                                "License Sync: Trial license record no longer exists on server. Clearing local premium state and resetting trial tombstone."
                            );
                            settings.HasClaimedTrial = false;
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }
                            return false;
                        }

                        if (wasUnlocked || !string.IsNullOrWhiteSpace(settings.PremiumLicenseKey))
                        {
                            Log.Information(
                                "License Sync: No active license key found on server for device {Hwid}. Revoking premium.",
                                myHwid
                            );
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }
                        }

                        return false;
                    }
                }

                return await VerifyPremiumStatusAsync(settings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error syncing assigned license with server.");
                return settings.IsPremiumUnlocked;
            }
            finally
            {
                _ = Interlocked.Exchange(ref _isSyncingAssignedLicense, 0);
            }
        }

        public async Task<bool> VerifyPremiumStatusAsync(AppSettings settings)
        {
            _lastSettings = settings;
            bool wasUnlocked = settings.IsPremiumUnlocked;

            if (string.IsNullOrEmpty(settings.PremiumActivationSignature))
            {
                if (string.IsNullOrWhiteSpace(settings.PremiumLicenseKey))
                {
                    ClearPremiumState(settings);
                    if (wasUnlocked)
                    {
                        PremiumStatusChanged?.Invoke(this, false);
                    }

                    return false;
                }
            }

            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                DateTime utcNow = DateTime.UtcNow;

                if (!IntegrityChecker.ValidateAssemblyIntegrity())
                {
                    Log.Warning("Binary tampering detected! Locking premium features.");
                    ClearPremiumState(settings);
                    if (wasUnlocked)
                    {
                        PremiumStatusChanged?.Invoke(this, false);
                    }

                    return false;
                }

                bool forceOnlineDueToClock = false;
                if (TryParseIso8601(settings.LastVerifiedUtc, out DateTime lastVerified))
                {
                    if (utcNow < lastVerified - ClockDriftTolerance)
                    {
                        if (settings.IsTrialLicense)
                        {
                            Log.Warning(
                                "Clock drift tampering detected on trial license! Current UTC: {UtcNow}, Last Verified: {LastVerified}. Immediately revoking trial access.",
                                utcNow,
                                lastVerified
                            );
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }

                            return false;
                        }

                        Log.Warning(
                            "System clock drift detected! Current UTC: {UtcNow}, Last Verified: {LastVerified}. Forcing online verification without erasing license.",
                            utcNow,
                            lastVerified
                        );
                        forceOnlineDueToClock = true;
                    }
                }

                if (!string.IsNullOrEmpty(settings.PremiumActivationSignature))
                {
                    if (!string.IsNullOrWhiteSpace(settings.PremiumExpiresAtUtc))
                    {
                        bool isHalloweenPermanent =
                            settings.PremiumLicenseKey?.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase) == true;

                        if (!isHalloweenPermanent)
                        {
                            if (!TryParseIso8601(settings.PremiumExpiresAtUtc, out DateTime expiresAt))
                            {
                                Log.Warning(
                                    "Invalid expiration date stored locally. Forcing online verification."
                                );
                                forceOnlineDueToClock = true;
                            }
                            else if (utcNow > expiresAt)
                            {
                                Log.Warning(
                                    "License expired locally. ExpiresAt: {ExpiresAt}, Current UTC: {UtcNow}. Revoking premium status.",
                                    expiresAt,
                                    utcNow
                                );
                                ClearPremiumState(settings);
                                if (wasUnlocked)
                                {
                                    PremiumStatusChanged?.Invoke(this, false);
                                }

                                return false;
                            }
                        }
                    }

                    string expectedPayload =
                        $"{hwid}:PREMIUM_UNLOCKED:{settings.PremiumExpiresAtUtc}";
                    bool isValidSignature = VerifyRsaSignature(
                        expectedPayload,
                        settings.PremiumActivationSignature
                    );

                    if (!isValidSignature)
                    {
                        Log.Warning(
                            "Premium activation signature validation failed. Premium features have been locked."
                        );
                        ClearPremiumState(settings);
                        if (wasUnlocked)
                        {
                            PremiumStatusChanged?.Invoke(this, false);
                        }

                        return false;
                    }
                }

                TimeSpan gracePeriod = settings.IsTrialLicense
                    ? TrialOfflineGracePeriod
                    : MaxOfflineGracePeriod;

                if (!string.IsNullOrWhiteSpace(settings.PremiumLicenseKey))
                {
                    bool hasLastOnline = TryParseIso8601(
                        settings.LastVerifiedUtc,
                        out DateTime lastOnline
                    );
                    bool mustGoOnline =
                        forceOnlineDueToClock
                        || !hasLastOnline
                        || (utcNow - lastOnline) > gracePeriod;
                    bool shouldRecheckOnline =
                        mustGoOnline || (utcNow - lastOnline) > OnlineRecheckInterval;

                    if (
                        !shouldRecheckOnline
                        && settings.IsPremiumUnlocked
                        && !string.IsNullOrEmpty(settings.PremiumActivationSignature)
                    )
                    {
                        return true;
                    }

                    try
                    {
                        Dictionary<string, string> payload = new()
                        {
                            { "licenseKey", settings.PremiumLicenseKey },
                            { "hwid", hwid },
                            { "nickname", settings.UserNickname },
                            { "action", "verify" },
                        };

                        using HttpRequestMessage req = new(HttpMethod.Post, ACTIVATION_URL)
                        {
                            Content = JsonContent.Create(payload),
                        };
                        if (!_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
                        {
                            _ = req.Headers.TryAddWithoutValidation(
                                "X-Api-Key",
                                Constants.GatekeeperApiKey
                            );
                        }

                        using HttpResponseMessage response = await _httpClient.SendAsync(req);

                        if (response.IsSuccessStatusCode)
                        {
                            ServerActivationResponse? result =
                                await response.Content.ReadFromJsonAsync<ServerActivationResponse>();

                            if (result != null && !result.Success)
                            {
                                Log.Warning(
                                    "Server reported premium license is no longer valid: {Error}. Revoking local premium status.",
                                    result.Error
                                );
                                ClearPremiumState(settings);
                                if (wasUnlocked)
                                {
                                    PremiumStatusChanged?.Invoke(this, false);
                                }

                                return false;
                            }

                            if (result != null && result.Success)
                            {
                                if (!string.IsNullOrEmpty(result.ExpiresAt))
                                {
                                    settings.PremiumExpiresAtUtc = result.ExpiresAt;
                                }
                                if (!string.IsNullOrEmpty(result.Signature))
                                {
                                    settings.PremiumActivationSignature = result.Signature;
                                }

                                settings.IsPremiumUnlocked = true;
                                if (!wasUnlocked)
                                {
                                    Log.Information(
                                        "License activated or restored! Unlocking premium features."
                                    );
                                    PremiumStatusChanged?.Invoke(this, true);
                                }
                            }

                            settings.LastVerifiedUtc = utcNow.ToString("O");
                            _lastSettings = settings;
                            _settingsService?.Save(settings);
                            return true;
                        }
                        else
                        {
                            if (
                                response.StatusCode
                                is HttpStatusCode.NotFound
                                    or HttpStatusCode.Forbidden
                            )
                            {
                                Log.Warning(
                                    "Server rejected license key (HTTP {StatusCode}). License no longer exists on server. Revoking premium.",
                                    response.StatusCode
                                );
                                ClearPremiumState(settings);
                                if (wasUnlocked)
                                {
                                    PremiumStatusChanged?.Invoke(this, false);
                                }
                                return false;
                            }

                            if (mustGoOnline)
                            {
                                Log.Warning(
                                    "Offline grace period ({Hours:F1} hours) exceeded and online verification returned HTTP {StatusCode}. Locking premium.",
                                    gracePeriod.TotalHours,
                                    response.StatusCode
                                );
                                ClearPremiumState(settings);
                                if (wasUnlocked)
                                {
                                    PremiumStatusChanged?.Invoke(this, false);
                                }

                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(
                            ex,
                            "Failed online verification for premium key. Falling back to local signature verification."
                        );

                        if (mustGoOnline)
                        {
                            Log.Warning(
                                "Offline grace period ({Hours:F1} hours) exceeded and online verification failed. Locking premium.",
                                gracePeriod.TotalHours
                            );
                            ClearPremiumState(settings);
                            if (wasUnlocked)
                            {
                                PremiumStatusChanged?.Invoke(this, false);
                            }

                            return false;
                        }
                    }
                }

                return settings.IsPremiumUnlocked;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to verify premium status.");
                ClearPremiumState(settings);
                if (wasUnlocked)
                {
                    PremiumStatusChanged?.Invoke(this, false);
                }

                return false;
            }
        }

        public async Task<PremiumActivationResult> StartFreeTrialAsync(string nickname)
        {
            if (!IntegrityChecker.ValidateAssemblyIntegrity())
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "Application integrity check failed. Please contact support.",
                };
            }

            _lastSettings ??= _settingsService.Load();
            if (_lastSettings?.HasClaimedTrial == true && !IsTrialActive)
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "Free trial has already been claimed on this device.",
                };
            }

            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                string cleanNickname = string.IsNullOrWhiteSpace(nickname)
                    ? (
                        string.IsNullOrWhiteSpace(_lastSettings?.UserNickname)
                            ? Environment.UserName
                            : _lastSettings.UserNickname
                    )
                    : nickname.Trim();

                if (cleanNickname.Length > 50)
                {
                    cleanNickname = cleanNickname[..50];
                }

                Dictionary<string, string> payload = new()
                {
                    { "action", "start_trial" },
                    { "hwid", hwid },
                    { "nickname", cleanNickname },
                };

                using HttpRequestMessage req = new(HttpMethod.Post, ACTIVATION_URL)
                {
                    Content = JsonContent.Create(payload),
                };
                if (!_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
                {
                    _ = req.Headers.TryAddWithoutValidation(
                        "X-Api-Key",
                        Constants.GatekeeperApiKey
                    );
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(req);

                string responseJson = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseJson);
                JsonElement root = doc.RootElement;

                bool success = root.TryGetProperty("success", out JsonElement s) && s.GetBoolean();
                string? error = root.TryGetProperty("error", out JsonElement e)
                    ? e.GetString()
                    : null;
                string? message = root.TryGetProperty("message", out JsonElement m)
                    ? m.GetString()
                    : null;
                string? code = root.TryGetProperty("code", out JsonElement c)
                    ? c.GetString()
                    : null;

                if (!response.IsSuccessStatusCode || !success)
                {
                    string errorMsg =
                        error ?? message ?? $"Server error (HTTP {(int)response.StatusCode}).";

                    if (
                        string.Equals(
                            code,
                            "TRIAL_ALREADY_CLAIMED",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || errorMsg.Contains(
                            "already been claimed",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || errorMsg.Contains("already claimed", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        _lastSettings ??= _settingsService.Load();
                        _lastSettings.HasClaimedTrial = true;
                        _settingsService.Save(_lastSettings);
                    }

                    return new PremiumActivationResult { Success = false, ErrorMessage = errorMsg };
                }

                string? key = root.TryGetProperty("key", out JsonElement k)
                    ? k.GetString()
                    : (
                        root.TryGetProperty("licenseKey", out JsonElement lk)
                            ? lk.GetString()
                            : null
                    );
                string? sig = root.TryGetProperty("signature", out JsonElement sg)
                    ? sg.GetString()
                    : null;
                string? expiresAt = root.TryGetProperty("expiresAt", out JsonElement exp)
                    ? exp.GetString()
                    : null;

                if (
                    string.IsNullOrEmpty(key)
                    || string.IsNullOrEmpty(sig)
                    || string.IsNullOrEmpty(expiresAt)
                )
                {
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = "Server returned incomplete trial credentials.",
                    };
                }

                string expectedPayload = $"{hwid}:PREMIUM_UNLOCKED:{expiresAt}";
                if (!VerifyRsaSignature(expectedPayload, sig))
                {
                    Log.Warning("RSA signature validation failed for trial activation response.");
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = "Server signature mismatch.",
                    };
                }

                if (
                    !TryParseIso8601(expiresAt, out DateTime expiresAtUtc)
                    || DateTime.UtcNow >= expiresAtUtc
                )
                {
                    Log.Warning(
                        "Trial expiration timestamp invalid or already expired: {ExpiresAt}",
                        expiresAt
                    );
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid trial expiration timestamp.",
                    };
                }

                _lastSettings ??= _settingsService.Load();
                _lastSettings.PremiumLicenseKey = key;
                _lastSettings.UserNickname = cleanNickname;
                _lastSettings.IsPremiumUnlocked = true;
                _lastSettings.IsTrialLicense = true;
                _lastSettings.HasClaimedTrial = true;
                _lastSettings.PremiumActivationSignature = sig;
                _lastSettings.PremiumExpiresAtUtc = expiresAt;
                _lastSettings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                _settingsService.Save(_lastSettings);

                PremiumStatusChanged?.Invoke(this, true);

                return new PremiumActivationResult
                {
                    Success = true,
                    Signature = sig,
                    ExpiresAt = expiresAt,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during free trial activation.");
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = $"Trial activation failed: {ex.Message}",
                };
            }
        }

        public async Task<SeasonalEventResult> ProcessSeasonalEventAsync(string nickname)
        {
            if (!IntegrityChecker.ValidateAssemblyIntegrity())
            {
                return new SeasonalEventResult
                {
                    Success = false,
                    ErrorMessage = "Application integrity check failed.",
                };
            }

            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                _lastSettings ??= _settingsService.Load();

                string cleanNickname = string.IsNullOrWhiteSpace(nickname)
                    ? (
                        string.IsNullOrWhiteSpace(_lastSettings?.UserNickname)
                            ? Environment.UserName
                            : _lastSettings.UserNickname
                    )
                    : nickname.Trim();

                if (cleanNickname.Length > 50)
                {
                    cleanNickname = cleanNickname[..50];
                }

                Dictionary<string, string> payload = new()
                {
                    { "action", "seasonal_sync" },
                    { "eventType", "halloween_title_click" },
                    { "hwid", hwid },
                    { "nickname", cleanNickname },
                };

                using HttpRequestMessage req = new(HttpMethod.Post, ACTIVATION_URL)
                {
                    Content = JsonContent.Create(payload),
                };
                if (!_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
                {
                    _ = req.Headers.TryAddWithoutValidation(
                        "X-Api-Key",
                        Constants.GatekeeperApiKey
                    );
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(req);
                string responseJson = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseJson);
                JsonElement root = doc.RootElement;

                bool success = root.TryGetProperty("success", out JsonElement s) && s.GetBoolean();
                string? dialogTitle = root.TryGetProperty("dialogTitle", out JsonElement dt)
                    ? dt.GetString()
                    : null;
                string? dialogBody = root.TryGetProperty("dialogBody", out JsonElement db)
                    ? db.GetString()
                    : null;
                string? closeBtn = root.TryGetProperty("closeButtonText", out JsonElement cb)
                    ? cb.GetString()
                    : null;
                string? notifTitle = root.TryGetProperty("notificationTitle", out JsonElement nt)
                    ? nt.GetString()
                    : null;
                string? notifMsg = root.TryGetProperty("notificationMessage", out JsonElement nm)
                    ? nm.GetString()
                    : null;
                string? appearance = root.TryGetProperty("appearance", out JsonElement ap)
                    ? ap.GetString()
                    : null;
                string? error = root.TryGetProperty("error", out JsonElement e)
                    ? e.GetString()
                    : null;

                if (!string.IsNullOrEmpty(dialogBody))
                {
                    dialogBody = SeasonalDurationRegex().Replace(dialogBody, "Lifetime Supporter");
                }
                if (!string.IsNullOrEmpty(dialogTitle))
                {
                    dialogTitle = SeasonalDurationRegex().Replace(dialogTitle, "Lifetime");
                }
                if (!string.IsNullOrEmpty(notifMsg))
                {
                    notifMsg = SeasonalDurationRegex().Replace(notifMsg, "Lifetime Supporter");
                }

                SeasonalEventResult result = new()
                {
                    Success = success,
                    DialogTitle = dialogTitle,
                    DialogBody = dialogBody,
                    CloseButtonText = closeBtn,
                    NotificationTitle = notifTitle,
                    NotificationMessage = notifMsg,
                    Appearance = appearance,
                    ErrorMessage = error,
                };

                if (!success)
                {
                    return result;
                }

                string? key = root.TryGetProperty("key", out JsonElement k)
                    ? k.GetString()
                    : (
                        root.TryGetProperty("licenseKey", out JsonElement lk)
                            ? lk.GetString()
                            : null
                    );
                string? sig = root.TryGetProperty("signature", out JsonElement sg)
                    ? sg.GetString()
                    : null;
                string? expiresAt = root.TryGetProperty("expiresAt", out JsonElement exp)
                    ? exp.GetString()
                    : null;

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(sig))
                {
                    string safeExpiresAt = expiresAt ?? string.Empty;
                    string expectedPayload = $"{hwid}:PREMIUM_UNLOCKED:{safeExpiresAt}";
                    bool isHalloweenKey = key.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase);
                    bool isExpired = !isHalloweenKey
                        && !string.IsNullOrEmpty(safeExpiresAt)
                        && (!TryParseIso8601(safeExpiresAt, out DateTime expUtc) || DateTime.UtcNow >= expUtc);

                    if (VerifyRsaSignature(expectedPayload, sig) && !isExpired)
                    {
                        _lastSettings ??= _settingsService.Load();
                        _lastSettings.PremiumLicenseKey = key;
                        _lastSettings.UserNickname = cleanNickname;
                        _lastSettings.IsPremiumUnlocked = true;
                        _lastSettings.IsTrialLicense = false;
                        _lastSettings.PremiumActivationSignature = sig;
                        _lastSettings.PremiumExpiresAtUtc = safeExpiresAt;
                        _lastSettings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                        _settingsService.Save(_lastSettings);

                        PremiumStatusChanged?.Invoke(this, true);

                        result.LicenseKey = key;
                        result.ExpiresAt = safeExpiresAt;
                        result.Signature = sig;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Seasonal event handling error.");
                return new SeasonalEventResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<PremiumActivationResult> ActivatePremiumKeyAsync(
            string licenseKey,
            string nickname
        )
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "License key cannot be empty.",
                };
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage = "Nickname cannot be empty.",
                };
            }

            if (!IntegrityChecker.ValidateAssemblyIntegrity())
            {
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage =
                        "Application integrity check failed. Please contact Noizy on discord - noizy3345.",
                };
            }

            try
            {
                string hwid = HardwareFingerprintHelper.GetMachineHwid();
                Dictionary<string, string> payload = new()
                {
                    { "licenseKey", licenseKey },
                    { "hwid", hwid },
                    { "nickname", nickname.Trim() },
                };

                using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    ACTIVATION_URL,
                    payload
                );

                ServerActivationResponse? result = null;
                try
                {
                    result = await response.Content.ReadFromJsonAsync<ServerActivationResponse>();
                }
                catch (Exception jsonEx)
                {
                    Log.Warning(jsonEx, "Could not parse JSON from server response.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg =
                        result?.Error
                        ?? result?.Message
                        ?? $"Server error (HTTP {(int)response.StatusCode}).";
                    Log.Error(
                        "Activation HTTP error {StatusCode}: {Error}",
                        response.StatusCode,
                        errorMsg
                    );
                    return new PremiumActivationResult { Success = false, ErrorMessage = errorMsg };
                }
                if (result == null)
                {
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid response from activation server.",
                    };
                }

                if (!result.Success)
                {
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = result.Error ?? "Activation failed.",
                    };
                }

                if (
                    string.IsNullOrEmpty(result.Payload)
                    || string.IsNullOrEmpty(result.Signature)
                    || !VerifyRsaSignature(result.Payload, result.Signature)
                )
                {
                    Log.Warning(
                        "RSA signature validation failed for server response. Payload: {Payload}, Signature: {Signature}",
                        result.Payload,
                        result.Signature
                    );
                    return new PremiumActivationResult
                    {
                        Success = false,
                        ErrorMessage = "Server signature mismatch.",
                    };
                }

                _lastSettings ??= _settingsService.Load();
                _lastSettings.PremiumLicenseKey = licenseKey.Trim();
                _lastSettings.UserNickname = nickname.Trim();
                _lastSettings.IsPremiumUnlocked = true;
                _lastSettings.IsTrialLicense = false;
                _lastSettings.PremiumActivationSignature = result.Signature ?? string.Empty;
                _lastSettings.PremiumExpiresAtUtc = result.ExpiresAt ?? string.Empty;
                _lastSettings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                _settingsService.Save(_lastSettings);
                PremiumStatusChanged?.Invoke(this, true);

                return new PremiumActivationResult
                {
                    Success = true,
                    Signature = result.Signature ?? string.Empty,
                    ExpiresAt = result.ExpiresAt ?? string.Empty,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Premium activation failed.");
                return new PremiumActivationResult
                {
                    Success = false,
                    ErrorMessage =
                        "An unexpected error occurred during activation. Please try again later.",
                };
            }
        }

        public void ClearPremiumState(AppSettings? settings = null)
        {
            settings ??= _lastSettings ?? _settingsService?.Load() ?? new AppSettings();
            bool wasUnlocked = settings.IsPremiumUnlocked;
            settings.IsPremiumUnlocked = false;
            settings.PremiumLicenseKey = string.Empty;
            settings.PremiumActivationSignature = string.Empty;
            settings.PremiumExpiresAtUtc = string.Empty;
            settings.IsTrialLicense = false;
            _cachedAssetKey = null;
            _lastSettings = settings;
            _settingsService?.Save(settings);
            if (wasUnlocked)
            {
                PremiumStatusChanged?.Invoke(this, false);
            }
        }

        public async Task<byte[]?> GetAssetDecryptionKeyAsync(bool forceRefresh = false)
        {
            _lastSettings ??= _settingsService.Load();

            if (
                _lastSettings == null
                || !IsPremiumUnlocked
            )
            {
                Log.Warning(
                    "AssetKey: Cannot request key - Premium is not unlocked."
                );
                return null;
            }

            if (!forceRefresh && _cachedAssetKey != null && _cachedAssetKey.Length == 32)
            {
                return _cachedAssetKey;
            }

            await _assetKeyLock.WaitAsync();
            try
            {
                if (!forceRefresh && _cachedAssetKey != null && _cachedAssetKey.Length == 32)
                {
                    return _cachedAssetKey;
                }

                string myHwid = HardwareFingerprintHelper.GetMachineHwid();
                string endpoint = $"{Constants.GatekeeperWorkerUrl}/license/asset-key";
                string keyToSend = _lastSettings.PremiumLicenseKey?.Trim() ?? string.Empty;

                var payload = new
                {
                    hwid = myHwid,
                    licenseKey = keyToSend,
                };

                using HttpRequestMessage req = new(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(payload),
                };

                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _ = req.Headers.TryAddWithoutValidation("User-Agent", "TextureSwapper/2.0");
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("X-Api-Key"))
                {
                    _ = req.Headers.TryAddWithoutValidation("X-Api-Key", Constants.GatekeeperApiKey);
                }

                using HttpResponseMessage response = await _httpClient.SendAsync(req);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    Log.Warning(
                        "AssetKey: Server returned status {Status} ({Error}) when fetching decryption key.",
                        response.StatusCode,
                        errContent
                    );
                    return null;
                }

                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync()
                );
                if (doc.RootElement.TryGetProperty("key", out JsonElement keyElem))
                {
                    string? rawKey = keyElem.GetString();
                    if (!string.IsNullOrEmpty(rawKey))
                    {
                        byte[]? keyBytes = null;
                        try
                        {
                            byte[] b64 = Convert.FromBase64String(rawKey);
                            if (b64.Length == 32)
                            {
                                keyBytes = b64;
                            }
                        }
                        catch { }

                        if (keyBytes == null && rawKey.Length == 64)
                        {
                            try
                            {
                                byte[] hex = Convert.FromHexString(rawKey);
                                if (hex.Length == 32)
                                {
                                    keyBytes = hex;
                                }
                            }
                            catch { }
                        }

                        if (keyBytes == null && rawKey.Length == 32)
                        {
                            keyBytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
                        }

                        if (keyBytes != null && keyBytes.Length == 32)
                        {
                            _cachedAssetKey = keyBytes;
                            Log.Information(
                                "AssetKey: Successfully fetched and cached 256-bit asset decryption key."
                            );
                            return _cachedAssetKey;
                        }
                    }
                }

                Log.Warning("AssetKey: Server response did not contain a valid 32-byte key.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AssetKey: Exception occurred while fetching decryption key.");
                return null;
            }
            finally
            {
                _ = _assetKeyLock.Release();
            }
        }

        internal static bool TryParseIso8601(string? dateStr, out DateTime result)
        {
            result = default;
            return !string.IsNullOrWhiteSpace(dateStr)
                && DateTime.TryParse(
                    dateStr,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out result
                );
        }

        internal static bool VerifyRsaSignature(
            string message,
            string signatureBase64,
            string? publicKeyPem = null
        )
        {
            if (string.IsNullOrWhiteSpace(signatureBase64) || string.IsNullOrEmpty(message))
            {
                return false;
            }

            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem ?? PUBLIC_KEY_PEM);

                return rsa.VerifyData(
                    messageBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RSA signature verification failed.");
                return false;
            }
        }

        [GeneratedRegex(@"\b(1[\s-]month|30[\s-]days?)\b", RegexOptions.IgnoreCase)]
        private static partial Regex SeasonalDurationRegex();
    }
}
