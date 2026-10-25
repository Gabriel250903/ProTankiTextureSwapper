using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TextureSwapper.Core.Models
{
    public class ServerLicenseKeyItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _licenseKey = string.Empty;
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

        private string _nickname = string.Empty;
        public string Nickname
        {
            get => _nickname;
            set
            {
                if (_nickname != value)
                {
                    _nickname = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanUnbind));
                }
            }
        }

        private string _hwid = string.Empty;
        public string Hwid
        {
            get => _hwid;
            set
            {
                if (_hwid != value)
                {
                    _hwid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanUnbind));
                }
            }
        }

        private string _status = "Inactive";
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanUnbind));
                    OnPropertyChanged(nameof(IsActive));
                    OnPropertyChanged(nameof(IsInactive));
                    OnPropertyChanged(nameof(IsRevoked));
                    OnPropertyChanged(nameof(IsExpired));
                }
            }
        }

        private string _expiresAt = string.Empty;
        public string ExpiresAt
        {
            get => _expiresAt;
            set
            {
                if (_expiresAt != value)
                {
                    _expiresAt = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedExpires));
                }
            }
        }

        private bool _isTrial;
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

        public bool IsActive =>
            Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
            || Status.Equals("Activated", StringComparison.OrdinalIgnoreCase);

        public bool IsInactive => Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);

        public bool IsRevoked => Status.Equals("Revoked", StringComparison.OrdinalIgnoreCase);

        public bool IsExpired => Status.Equals("Expired", StringComparison.OrdinalIgnoreCase);

        public string FormattedExpires
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExpiresAt))
                {
                    return "—";
                }

                if (
                    DateTime.TryParse(
                        ExpiresAt,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime expUtc
                    )
                )
                {
                    TimeSpan rem = expUtc - DateTime.UtcNow;
                    string remStr =
                        rem.TotalSeconds > 0
                            ? (
                                rem.TotalDays >= 1
                                    ? $" ({(int)rem.TotalDays}d left)"
                                    : $" ({(int)rem.TotalHours}h left)"
                            )
                            : " (Expired)";
                    return $"{expUtc.ToLocalTime():yyyy-MM-dd}{remStr}";
                }
                return ExpiresAt;
            }
        }

        public bool CanUnbind =>
            !string.IsNullOrWhiteSpace(Hwid)
            && (
                Status.Equals("Activated", StringComparison.OrdinalIgnoreCase)
                || Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
            );
    }

    public class ServerKeysListResponse
    {
        public bool Success { get; set; }
        public List<ServerLicenseKeyItem> Keys { get; set; } = [];
    }
}
