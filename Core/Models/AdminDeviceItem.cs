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
                }
            }
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
