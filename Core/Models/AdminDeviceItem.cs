using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TextureSwapper.Core.Models
{
    public class AdminDeviceItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _hwid = string.Empty;

        [JsonPropertyName("hwid")]
        public string Hwid
        {
            get => _hwid;
            set
            {
                if (_hwid != value)
                {
                    _hwid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShortHwid));
                }
            }
        }

        private string _firstSeen = string.Empty;

        [JsonPropertyName("first_seen")]
        public string FirstSeen
        {
            get => _firstSeen;
            set
            {
                if (_firstSeen != value)
                {
                    _firstSeen = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _lastSeen = string.Empty;

        [JsonPropertyName("last_seen")]
        public string LastSeen
        {
            get => _lastSeen;
            set
            {
                if (_lastSeen != value)
                {
                    _lastSeen = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _ipAddress = string.Empty;

        [JsonPropertyName("ip")]
        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                if (_ipAddress != value)
                {
                    _ipAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isBanned;

        [JsonPropertyName("is_banned")]
        public bool IsBanned
        {
            get => _isBanned;
            set
            {
                if (_isBanned != value)
                {
                    _isBanned = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(IsPermanentBan));
                    OnPropertyChanged(nameof(FormattedBanExpires));
                }
            }
        }

        private string _banReason = string.Empty;

        [JsonPropertyName("ban_reason")]
        public string BanReason
        {
            get => _banReason;
            set
            {
                if (_banReason != value)
                {
                    _banReason = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isTempBan;

        [JsonPropertyName("is_temp_ban")]
        public bool IsTempBan
        {
            get => _isTempBan;
            set
            {
                if (_isTempBan != value)
                {
                    _isTempBan = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(IsPermanentBan));
                    OnPropertyChanged(nameof(FormattedBanExpires));
                }
            }
        }

        private string _banExpiresAt = string.Empty;

        [JsonPropertyName("ban_expires_at")]
        public string BanExpiresAt
        {
            get => _banExpiresAt;
            set
            {
                if (_banExpiresAt != value)
                {
                    _banExpiresAt = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(FormattedBanExpires));
                }
            }
        }

        private bool _isTrial;

        [JsonPropertyName("is_trial")]
        public bool IsTrial
        {
            get => _isTrial;
            set
            {
                if (_isTrial != value)
                {
                    _isTrial = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UserDisplay));
                }
            }
        }

        private string _nickname = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname
        {
            get
            {
                if (!string.IsNullOrEmpty(_nickname)) return _nickname;
                if (!string.IsNullOrEmpty(_ipAddress))
                {
                    if (_ipAddress.StartsWith("Trial: ", StringComparison.OrdinalIgnoreCase))
                        return _ipAddress[7..].Trim();
                    if (_ipAddress.StartsWith("User: ", StringComparison.OrdinalIgnoreCase))
                        return _ipAddress[6..].Trim();
                }
                return string.Empty;
            }
            set
            {
                if (_nickname != value)
                {
                    _nickname = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UserDisplay));
                }
            }
        }

        private string _licenseKey = string.Empty;

        [JsonPropertyName("license_key")]
        public string LicenseKey
        {
            get => _licenseKey;
            set
            {
                if (_licenseKey != value)
                {
                    _licenseKey = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _tier = string.Empty;

        [JsonPropertyName("tier")]
        public string Tier
        {
            get
            {
                if (!string.IsNullOrEmpty(_tier)) return _tier;
                if (IsTrial || (!string.IsNullOrEmpty(_ipAddress) && _ipAddress.StartsWith("Trial:", StringComparison.OrdinalIgnoreCase)))
                    return "3-Day Free Trial";
                if (!string.IsNullOrEmpty(_ipAddress) && _ipAddress.StartsWith("User:", StringComparison.OrdinalIgnoreCase))
                    return "Lifetime Supporter";
                if (string.Equals(_lastSeen, "Free User", StringComparison.OrdinalIgnoreCase) || string.Equals(_ipAddress, "Free User", StringComparison.OrdinalIgnoreCase))
                    return "Free User";
                return "Free User";
            }
            set
            {
                if (_tier != value)
                {
                    _tier = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UserDisplay));
                }
            }
        }

        private string _location = string.Empty;

        [JsonPropertyName("location")]
        public string Location
        {
            get => _location;
            set
            {
                if (_location != value)
                {
                    _location = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public string DisplayIpAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_ipAddress) ||
                    _ipAddress.StartsWith("User:", StringComparison.OrdinalIgnoreCase) ||
                    _ipAddress.StartsWith("Trial:", StringComparison.OrdinalIgnoreCase) ||
                    _ipAddress.Equals("Free User", StringComparison.OrdinalIgnoreCase) ||
                    _ipAddress.Equals("Banned Device", StringComparison.OrdinalIgnoreCase))
                {
                    return "Unknown";
                }
                return _ipAddress;
            }
        }

        [JsonIgnore]
        public string DisplayLastSeen
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_lastSeen) ||
                    _lastSeen.Equals("Free User", StringComparison.OrdinalIgnoreCase) ||
                    _lastSeen.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                    _lastSeen.Equals("Banned", StringComparison.OrdinalIgnoreCase) ||
                    _lastSeen.StartsWith("Expires:", StringComparison.OrdinalIgnoreCase) ||
                    _lastSeen.StartsWith("Ban Exp:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(_firstSeen) &&
                        DateTime.TryParse(_firstSeen, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime fsUtc))
                    {
                        return FormatTimestamp(fsUtc);
                    }
                    return "Recent";
                }

                if (DateTime.TryParse(_lastSeen, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime dtUtc))
                {
                    return FormatTimestamp(dtUtc);
                }
                return _lastSeen;
            }
        }

        [JsonIgnore]
        public string UserDisplay
        {
            get
            {
                string nick = Nickname;
                string t = Tier;
                if (!string.IsNullOrWhiteSpace(nick))
                {
                    return $"{nick} • {t}";
                }
                return t;
            }
        }

        private static string FormatTimestamp(DateTime utc)
        {
            TimeSpan elapsed = DateTime.UtcNow - utc;
            if (elapsed.TotalMinutes < 1) return "Just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        [JsonIgnore]
        public string ShortHwid =>
            string.IsNullOrEmpty(Hwid) || Hwid.Length < 16 ? Hwid : $"{Hwid[..8]}...{Hwid[^8..]}";

        [JsonIgnore]
        public bool IsPermanentBan => IsBanned && !IsTempBan;

        [JsonIgnore]
        public string FormattedBanExpires
        {
            get
            {
                if (!IsTempBan || string.IsNullOrWhiteSpace(BanExpiresAt))
                {
                    return string.Empty;
                }

                if (
                    DateTime.TryParse(
                        BanExpiresAt,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime expUtc
                    )
                )
                {
                    DateTime localExp = expUtc.ToLocalTime();
                    TimeSpan rem = expUtc - DateTime.UtcNow;
                    string remStr =
                        rem.TotalSeconds > 0
                            ? (
                                rem.TotalDays >= 1
                                    ? $" ({(int)rem.TotalDays}d {rem.Hours}h remaining)"
                                    : $" ({(int)rem.TotalHours}h remaining)"
                            )
                            : " (Expired)";
                    return $"{localExp:yyyy-MM-dd HH:mm}{remStr}";
                }
                return BanExpiresAt;
            }
        }

        [JsonIgnore]
        public string StatusDisplay
        {
            get
            {
                if (!IsBanned)
                {
                    return "Active";
                }

                if (IsTempBan)
                {
                    if (
                        DateTime.TryParse(
                            BanExpiresAt,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out DateTime expUtc
                        )
                    )
                    {
                        TimeSpan remaining = expUtc - DateTime.UtcNow;
                        return remaining.TotalSeconds <= 0 ? "Temp Ban (Expired)"
                            : remaining.TotalDays >= 1
                                ? $"Temp Ban ({(int)remaining.TotalDays}d {remaining.Hours}h left)"
                            : remaining.TotalHours >= 1
                                ? $"Temp Ban ({(int)remaining.TotalHours}h left)"
                            : $"Temp Ban ({(int)remaining.TotalMinutes}m left)";
                    }
                    return "Temp Banned";
                }
                return "Permanently Banned";
            }
        }
    }
}
