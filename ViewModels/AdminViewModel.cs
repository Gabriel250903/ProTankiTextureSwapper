using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly INotificationService _notificationService;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public event Action? CloseRequested;
        public event Func<string, string, ControlAppearance, Task>? AdminNotificationRequested;

        public string CurrentHardwareId =>
            _mainViewModel.DeviceBanService?.GetDeviceHardwareId()
            ?? HardwareFingerprintHelper.GetMachineHwid();

        public bool RememberPassword
        {
            get => _mainViewModel.Settings.RememberAdminPassword;
            set
            {
                if (_mainViewModel.Settings.RememberAdminPassword != value)
                {
                    _mainViewModel.Settings.RememberAdminPassword = value;
                    OnPropertyChanged();
                    _mainViewModel.SaveSettings();
                }
            }
        }

        public bool EnableHalloweenSound
        {
            get => _mainViewModel.Settings.EnableHalloweenSound;
            set
            {
                if (_mainViewModel.Settings.EnableHalloweenSound != value)
                {
                    _mainViewModel.Settings.EnableHalloweenSound = value;
                    OnPropertyChanged();
                    _mainViewModel.SaveSettings();
                    if (value)
                    {
                        _mainViewModel.HalloweenSoundService.PlayHollowClick();
                        if (_mainViewModel.IsHalloweenThemeActive)
                        {
                            _mainViewModel.HalloweenSoundService.SetAmbientDrone(true);
                        }
                    }
                    else
                    {
                        _mainViewModel.HalloweenSoundService.SetAmbientDrone(false);
                    }
                }
            }
        }

        public bool ForceHalloweenPreview
        {
            get => _mainViewModel.Settings.ForceHalloweenPreview;
            set
            {
                if (_mainViewModel.Settings.ForceHalloweenPreview != value)
                {
                    _mainViewModel.Settings.ForceHalloweenPreview = value;
                    OnPropertyChanged();
                    _mainViewModel.SaveSettings();
                    _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
                    OnPropertyChanged(nameof(IsHalloweenThemeActive));
                    OnPropertyChanged(nameof(HalloweenStatusText));
                }
            }
        }

        public bool IsHalloweenThemeActive =>
            _mainViewModel.HalloweenThemeService.IsHalloweenActive;

        public string HalloweenStatusText =>
            _mainViewModel.HalloweenThemeService.IsHalloweenActive
                ? _mainViewModel.Settings.ForceHalloweenPreview
                    ? "Active (Preview Mode)"
                    : "Active (Halloween Week Window)"
                : "Inactive (Outside Oct 31 - Nov 7 Window)";

        public bool SkipRemoteTargetSync
        {
            get => _mainViewModel.Settings.SkipRemoteTargetSync;
            set
            {
                if (_mainViewModel.Settings.SkipRemoteTargetSync != value)
                {
                    _mainViewModel.Settings.SkipRemoteTargetSync = value;
                    OnPropertyChanged();
                    _mainViewModel.SaveSettings();
                }
            }
        }

        private string _passwordInput = string.Empty;
        public string PasswordInput
        {
            get => _passwordInput;
            set => SetProperty(ref _passwordInput, value);
        }

        private bool _isAuthenticated;
        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                if (SetProperty(ref _isAuthenticated, value))
                {
                    if (value)
                    {
                        _ = LoadPaintsAsync();
                        _ = RefreshKeysAsync();
                        _ = RefreshDevicesAsync();
                        RefreshLogs();
                    }
                }
            }
        }

        private string _selectedTab = "Paints";
        public bool IsPaintsTabSelected => _selectedTab == "Paints";
        public bool IsLicensesTabSelected => _selectedTab == "Licenses";
        public bool IsBansTabSelected => _selectedTab == "Bans";
        public bool IsLogsTabSelected => _selectedTab == "Logs";

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterPaints();
                }
            }
        }

        private string _newCategoryName = string.Empty;
        public string NewCategoryName
        {
            get => _newCategoryName;
            set => SetProperty(ref _newCategoryName, value);
        }

        private string _selectedCategoryToRemove = string.Empty;
        public string SelectedCategoryToRemove
        {
            get => _selectedCategoryToRemove;
            set => SetProperty(ref _selectedCategoryToRemove, value);
        }

        private string _selectedCategoryToRename = string.Empty;
        public string SelectedCategoryToRename
        {
            get => _selectedCategoryToRename;
            set => SetProperty(ref _selectedCategoryToRename, value);
        }

        private string _renameCategoryNewName = string.Empty;
        public string RenameCategoryNewName
        {
            get => _renameCategoryNewName;
            set => SetProperty(ref _renameCategoryNewName, value);
        }

        private string _selectedFilterCategory = "All";
        public string SelectedFilterCategory
        {
            get => _selectedFilterCategory;
            set
            {
                if (SetProperty(ref _selectedFilterCategory, value))
                {
                    FilterPaints();
                }
            }
        }

        private List<SkinModel> _allPaints = [];
        public ObservableCollection<SkinModel> FilteredPaints { get; } = [];
        public ObservableCollection<string> ExistingItemNames { get; } = [];
        public ObservableCollection<string> FilterCategories { get; } = [];

        private string _licenseSearchText = string.Empty;
        public string LicenseSearchText
        {
            get => _licenseSearchText;
            set
            {
                if (SetProperty(ref _licenseSearchText, value))
                {
                    CurrentKeysPage = 1;
                    UpdatePagedKeys();
                }
            }
        }

        public ObservableCollection<string> LicenseStatusFilters { get; } =
        ["All", "Activated", "Active", "Inactive", "Revoked"];

        private string _selectedLicenseStatusFilter = "All";
        public string SelectedLicenseStatusFilter
        {
            get => _selectedLicenseStatusFilter;
            set
            {
                if (SetProperty(ref _selectedLicenseStatusFilter, value))
                {
                    CurrentKeysPage = 1;
                    UpdatePagedKeys();
                }
            }
        }

        private List<ServerLicenseKeyItem> _allServerKeys = [];
        private readonly Dictionary<string, DateTime> _locallyDeletedKeys = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<
            string,
            (string Status, string Hwid, string Nickname, DateTime Timestamp)
        > _recentLocalKeyOverrides = new(StringComparer.OrdinalIgnoreCase);
        public ObservableCollection<ServerLicenseKeyItem> PagedServerKeys { get; } = [];

        private int _totalServerKeysCount;
        public int TotalServerKeysCount
        {
            get => _totalServerKeysCount;
            set => SetProperty(ref _totalServerKeysCount, value);
        }

        private int _currentKeysPage = 1;
        public int CurrentKeysPage
        {
            get => _currentKeysPage;
            set
            {
                if (SetProperty(ref _currentKeysPage, value))
                {
                    UpdatePagedKeys();
                }
            }
        }

        private int _totalKeysPages = 1;
        public int TotalKeysPages
        {
            get => _totalKeysPages;
            set => SetProperty(ref _totalKeysPages, value);
        }

        private bool _isLoadingKeys;
        public bool IsLoadingKeys
        {
            get => _isLoadingKeys;
            set => SetProperty(ref _isLoadingKeys, value);
        }

        private string _sortColumn = "status";
        private bool _sortAscending = true;
        public string SortIndicatorLicenseKey =>
            _sortColumn == "key" ? (_sortAscending ? " ▲" : " ▼") : string.Empty;
        public string SortIndicatorNickname =>
            _sortColumn == "nickname" ? (_sortAscending ? " ▲" : " ▼") : string.Empty;
        public string SortIndicatorHwid =>
            _sortColumn == "hwid" ? (_sortAscending ? " ▲" : " ▼") : string.Empty;
        public string SortIndicatorStatus =>
            _sortColumn == "status" ? (_sortAscending ? " ▲" : " ▼") : string.Empty;

        private string _deviceFilterText = string.Empty;
        public string DeviceFilterText
        {
            get => _deviceFilterText;
            set
            {
                if (SetProperty(ref _deviceFilterText, value))
                {
                    FilterDevices();
                }
            }
        }

        private List<AdminDeviceItem> _allDevices = [];
        private readonly Dictionary<
            string,
            (
                bool IsBanned,
                bool IsTempBan,
                string BanReason,
                string BanExpiresAt,
                DateTime Timestamp
            )
        > _recentLocalDeviceOverrides = new(StringComparer.OrdinalIgnoreCase);
        public ObservableCollection<AdminDeviceItem> FilteredDevices { get; } = [];

        private bool _isDevicesLoading;
        public bool IsDevicesLoading
        {
            get => _isDevicesLoading;
            set => SetProperty(ref _isDevicesLoading, value);
        }

        private string _banReasonInput = "Violating Terms of Service";
        public string BanReasonInput
        {
            get => _banReasonInput;
            set => SetProperty(ref _banReasonInput, value);
        }

        private bool _isDeviceProfileVisible;
        public bool IsDeviceProfileVisible
        {
            get => _isDeviceProfileVisible;
            set => SetProperty(ref _isDeviceProfileVisible, value);
        }

        private AdminDeviceItem? _selectedProfileDevice;
        public AdminDeviceItem? SelectedProfileDevice
        {
            get => _selectedProfileDevice;
            set
            {
                if (SetProperty(ref _selectedProfileDevice, value))
                {
                    UpdateProfileKey();
                }
            }
        }

        private ServerLicenseKeyItem? _selectedProfileKey;
        public ServerLicenseKeyItem? SelectedProfileKey
        {
            get => _selectedProfileKey;
            set => SetProperty(ref _selectedProfileKey, value);
        }

        private bool _hasProfileKey;
        public bool HasProfileKey
        {
            get => _hasProfileKey;
            set => SetProperty(ref _hasProfileKey, value);
        }

        private bool _isAssignKeyModalVisible;
        public bool IsAssignKeyModalVisible
        {
            get => _isAssignKeyModalVisible;
            set => SetProperty(ref _isAssignKeyModalVisible, value);
        }

        private ServerLicenseKeyItem? _targetAssignKeyItem;
        public ServerLicenseKeyItem? TargetAssignKeyItem
        {
            get => _targetAssignKeyItem;
            set => SetProperty(ref _targetAssignKeyItem, value);
        }

        private string _assignSearchText = string.Empty;
        public string AssignSearchText
        {
            get => _assignSearchText;
            set
            {
                if (SetProperty(ref _assignSearchText, value))
                {
                    FilterAssignDevices();
                }
            }
        }

        private AdminDeviceItem? _selectedAssignDevice;
        public AdminDeviceItem? SelectedAssignDevice
        {
            get => _selectedAssignDevice;
            set
            {
                if (SetProperty(ref _selectedAssignDevice, value))
                {
                    OnPropertyChanged(nameof(CanConfirmAssignKey));
                }
            }
        }

        private string _assignNickname = string.Empty;
        public string AssignNickname
        {
            get => _assignNickname;
            set
            {
                if (SetProperty(ref _assignNickname, value))
                {
                    OnPropertyChanged(nameof(CanConfirmAssignKey));
                }
            }
        }

        public bool CanConfirmAssignKey => SelectedAssignDevice != null && !IsAssigningKey;

        private bool _isAssigningKey;
        public bool IsAssigningKey
        {
            get => _isAssigningKey;
            set
            {
                if (SetProperty(ref _isAssigningKey, value))
                {
                    OnPropertyChanged(nameof(CanConfirmAssignKey));
                }
            }
        }

        public ObservableCollection<AdminDeviceItem> FilteredAssignDevices { get; } = [];

        private string _adminLogContent = string.Empty;
        public string AdminLogContent
        {
            get => _adminLogContent;
            set => SetProperty(ref _adminLogContent, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectTabCommand { get; }
        public ICommand CopyKeyCommand { get; }
        public ICommand CopyHwidCommand { get; }

        public ICommand AddCategoryCommand { get; }
        public ICommand RemoveCategoryCommand { get; }
        public ICommand RenameCategoryCommand { get; }
        public ICommand SaveChangesCommand { get; }

        public ICommand RefreshKeysCommand { get; }
        public ICommand GenerateKeyCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand PrevKeysPageCommand { get; }
        public ICommand NextKeysPageCommand { get; }
        public ICommand OpenAssignKeyCommand { get; }
        public ICommand RevokeKeyCommand { get; }
        public ICommand UnbindHwidCommand { get; }
        public ICommand DeleteKeyCommand { get; }

        public ICommand RefreshDevicesCommand { get; }
        public ICommand Ban1DayCommand { get; }
        public ICommand Ban7DaysCommand { get; }
        public ICommand BanPermanentCommand { get; }
        public ICommand UnbanDeviceCommand { get; }

        public ICommand ViewDeviceProfileCommand { get; }
        public ICommand CloseDeviceProfileCommand { get; }
        public ICommand ProfileAssignKeyCommand { get; }
        public ICommand CloseAssignKeyModalCommand { get; }
        public ICommand SelectMyDeviceForAssignCommand { get; }
        public ICommand ConfirmAssignKeyCommand { get; }

        public ICommand RevokeTrialCommand { get; }
        public ICommand ResetTrialCommand { get; }
        public ICommand ExtendTrialCommand { get; }

        public ICommand RefreshAdminLogsCommand { get; }
        public ICommand ClearAdminLogsCommand { get; }
        public ICommand CopyAdminLogsCommand { get; }
        public ICommand OpenAdminLogsFolderCommand { get; }

        public AdminViewModel(MainViewModel mainViewModel, INotificationService notificationService)
        {
            _mainViewModel = mainViewModel;
            _notificationService = notificationService;

            LoginCommand = new AsyncRelayCommand(ExecuteLogin);
            LogoutCommand = new RelayCommand(_ =>
            {
                AdminSession.ClearSession();
                IsAuthenticated = false;
                _mainViewModel.Settings.SavedAdminSecretEncrypted = string.Empty;
                _mainViewModel.HalloweenThemeService.CheckAndApply();
                OnPropertyChanged(nameof(IsHalloweenThemeActive));
                OnPropertyChanged(nameof(HalloweenStatusText));
                _mainViewModel.SaveSettings();
            });
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
            SelectTabCommand = new RelayCommand(p =>
            {
                if (p is string tab)
                {
                    _selectedTab = tab;
                    OnPropertyChanged(nameof(IsPaintsTabSelected));
                    OnPropertyChanged(nameof(IsLicensesTabSelected));
                    OnPropertyChanged(nameof(IsBansTabSelected));
                    OnPropertyChanged(nameof(IsLogsTabSelected));

                    if (IsLicensesTabSelected)
                    {
                        _ = RefreshKeysAsync();
                    }
                    else if (IsBansTabSelected)
                    {
                        _ = RefreshDevicesAsync();
                    }
                    else if (IsLogsTabSelected)
                    {
                        RefreshLogs();
                    }
                }
            });

            CopyKeyCommand = new RelayCommand(p =>
            {
                string text = p?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        Clipboard.SetText(text);
                        ShowNotificationSafe(
                            "Copied",
                            "Copied to clipboard.",
                            ControlAppearance.Success
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to copy to clipboard.");
                    }
                }
            });

            CopyHwidCommand = new RelayCommand(p =>
            {
                string hwid = p is AdminDeviceItem item ? item.Hwid : p?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(hwid))
                {
                    try
                    {
                        Clipboard.SetText(hwid);
                        ShowNotificationSafe(
                            "Copied",
                            "Hardware ID copied to clipboard.",
                            ControlAppearance.Success
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to copy HWID to clipboard.");
                    }
                }
            });

            AddCategoryCommand = new RelayCommand(ExecuteAddCategory);
            RemoveCategoryCommand = new RelayCommand(ExecuteRemoveCategory);
            RenameCategoryCommand = new RelayCommand(ExecuteRenameCategory);
            SaveChangesCommand = new AsyncRelayCommand(ExecuteSaveChanges);

            RefreshKeysCommand = new AsyncRelayCommand(async _ => await RefreshKeysAsync(true));
            GenerateKeyCommand = new AsyncRelayCommand(ExecuteGenerateKey);
            ExportCsvCommand = new RelayCommand(ExecuteExportCsv);
            SortCommand = new RelayCommand(ExecuteSort);
            PrevKeysPageCommand = new RelayCommand(_ =>
            {
                if (CurrentKeysPage > 1)
                {
                    CurrentKeysPage--;
                }
            });
            NextKeysPageCommand = new RelayCommand(_ =>
            {
                if (CurrentKeysPage < TotalKeysPages)
                {
                    CurrentKeysPage++;
                }
            });
            OpenAssignKeyCommand = new RelayCommand(ExecuteOpenAssignKeyModal);
            RevokeKeyCommand = new AsyncRelayCommand(ExecuteRevokeKey);
            UnbindHwidCommand = new AsyncRelayCommand(ExecuteUnbindHwid);
            DeleteKeyCommand = new AsyncRelayCommand(ExecuteDeleteKey);

            RefreshDevicesCommand = new AsyncRelayCommand(async _ =>
                await RefreshDevicesAsync(true)
            );
            Ban1DayCommand = new AsyncRelayCommand(async p => await ExecuteBanAsync(p, 24));
            Ban7DaysCommand = new AsyncRelayCommand(async p => await ExecuteBanAsync(p, 168));
            BanPermanentCommand = new AsyncRelayCommand(async p => await ExecuteBanAsync(p, 0));
            UnbanDeviceCommand = new AsyncRelayCommand(ExecuteUnbanDevice);

            ViewDeviceProfileCommand = new RelayCommand(p =>
            {
                if (p is AdminDeviceItem dev)
                {
                    SelectedProfileDevice = dev;
                    IsDeviceProfileVisible = true;
                }
            });
            CloseDeviceProfileCommand = new RelayCommand(_ => IsDeviceProfileVisible = false);
            ProfileAssignKeyCommand = new RelayCommand(_ =>
            {
                if (SelectedProfileDevice != null)
                {
                    SelectedAssignDevice = SelectedProfileDevice;
                    AssignNickname = SelectedProfileKey?.Nickname ?? string.Empty;
                    TargetAssignKeyItem = null;
                    IsAssignKeyModalVisible = true;
                }
            });
            CloseAssignKeyModalCommand = new RelayCommand(_ => IsAssignKeyModalVisible = false);
            SelectMyDeviceForAssignCommand = new RelayCommand(_ =>
            {
                string myHwid = CurrentHardwareId;
                AdminDeviceItem myDevice =
                    _allDevices.FirstOrDefault(d =>
                        d.Hwid.Equals(myHwid, StringComparison.OrdinalIgnoreCase)
                    ) ?? new AdminDeviceItem { Hwid = myHwid, IpAddress = "This PC" };

                AdminDeviceItem? inList = FilteredAssignDevices.FirstOrDefault(d =>
                    d.Hwid.Equals(myHwid, StringComparison.OrdinalIgnoreCase)
                );
                if (inList == null)
                {
                    FilteredAssignDevices.Insert(0, myDevice);
                    SelectedAssignDevice = myDevice;
                }
                else
                {
                    SelectedAssignDevice = inList;
                }

                if (string.IsNullOrWhiteSpace(AssignNickname))
                {
                    AssignNickname = !string.IsNullOrWhiteSpace(
                        _mainViewModel.Settings.UserNickname
                    )
                        ? _mainViewModel.Settings.UserNickname
                        : Environment.UserName;
                }
            });
            ConfirmAssignKeyCommand = new AsyncRelayCommand(ExecuteConfirmAssignKey);

            RevokeTrialCommand = new AsyncRelayCommand(ExecuteRevokeTrialAsync);
            ResetTrialCommand = new AsyncRelayCommand(ExecuteResetTrialAsync);
            ExtendTrialCommand = new AsyncRelayCommand(async p =>
                await ExecuteExtendTrialAsync(p, 3)
            );

            RefreshAdminLogsCommand = new RelayCommand(_ => RefreshLogs());
            ClearAdminLogsCommand = new RelayCommand(_ => AdminLogContent = string.Empty);
            CopyAdminLogsCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(AdminLogContent))
                {
                    try
                    {
                        Clipboard.SetText(AdminLogContent);
                        ShowNotificationSafe(
                            "Copied",
                            "Logs copied to clipboard.",
                            ControlAppearance.Success
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to copy logs.");
                    }
                }
            });
            OpenAdminLogsFolderCommand = new RelayCommand(_ =>
            {
                string logFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.LogsDir
                );
                if (Directory.Exists(logFolder))
                {
                    _ = System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logFolder,
                            UseShellExecute = true,
                        }
                    );
                }
            });
        }

        public async Task<bool> TryAutoLoginAsync()
        {
            if (IsAuthenticated)
            {
                return true;
            }

            if (
                !_mainViewModel.Settings.RememberAdminPassword
                || string.IsNullOrEmpty(_mainViewModel.Settings.SavedAdminSecretEncrypted)
            )
            {
                return false;
            }

            try
            {
                string? secret = AdminSecretStorageHelper.DecryptSecret(
                    _mainViewModel.Settings.SavedAdminSecretEncrypted
                );
                if (string.IsNullOrEmpty(secret))
                {
                    return false;
                }

                (bool success, string _) =
                    await _mainViewModel.DeviceBanService.AuthenticateAdminAsync(secret);
                if (success)
                {
                    AdminSession.SetSession(secret);
                    IsAuthenticated = true;
                    _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
                    OnPropertyChanged(nameof(IsHalloweenThemeActive));
                    OnPropertyChanged(nameof(HalloweenStatusText));
                    Log.Information("Admin auto-login successful via saved credentials.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed auto-login attempt.");
            }
            return false;
        }

        private async Task ExecuteLogin(object? parameter)
        {
            string password =
                parameter switch
                {
                    System.Windows.Controls.PasswordBox pb => pb.Password,
                    Wpf.Ui.Controls.PasswordBox wpb => wpb.Password,
                    string s => s,
                    _ => PasswordInput,
                } ?? string.Empty;

            string trimmedInput = password.Trim();
            if (string.IsNullOrEmpty(trimmedInput))
            {
                ShowNotificationSafe(
                    "Error",
                    "Password cannot be empty.",
                    ControlAppearance.Danger
                );
                return;
            }

            try
            {
                (bool success, string message) =
                    await _mainViewModel.DeviceBanService.AuthenticateAdminAsync(trimmedInput);
                if (success)
                {
                    AdminSession.SetSession(trimmedInput);
                    if (RememberPassword)
                    {
                        _mainViewModel.Settings.SavedAdminSecretEncrypted =
                            AdminSecretStorageHelper.EncryptSecret(trimmedInput);
                        _mainViewModel.SaveSettings();
                    }
                    else
                    {
                        _mainViewModel.Settings.SavedAdminSecretEncrypted = string.Empty;
                        _mainViewModel.SaveSettings();
                    }

                    IsAuthenticated = true;
                    PasswordInput = string.Empty;
                    _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
                    OnPropertyChanged(nameof(IsHalloweenThemeActive));
                    OnPropertyChanged(nameof(HalloweenStatusText));
                    Log.Information("Admin successfully authenticated.");
                }
                else
                {
                    IsAuthenticated = false;
                    ShowNotificationSafe(
                        "Authentication Failed",
                        string.IsNullOrWhiteSpace(message) ? "Incorrect admin password." : message,
                        ControlAppearance.Danger
                    );
                    Log.Warning("Failed admin authentication attempt.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Admin login error.");
                ShowNotificationSafe(
                    "Error",
                    $"Authentication error: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
        }

        private async Task LoadPaintsAsync()
        {
            try
            {
                string jsonPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.PaintsSkinsJson
                );
                if (File.Exists(jsonPath))
                {
                    string json = await File.ReadAllTextAsync(jsonPath);
                    _allPaints = await Task.Run(() =>
                        JsonSerializer.Deserialize<List<SkinModel>>(json) ?? []
                    );
                }
                else
                {
                    _allPaints = [];
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    List<string> names =
                    [
                        .. _allPaints
                            .Select(p => p.ItemName)
                            .Distinct()
                            .Where(n => !string.IsNullOrEmpty(n))
                            .OrderBy(n => n),
                    ];
                    ExistingItemNames.Clear();
                    foreach (string name in names)
                    {
                        ExistingItemNames.Add(name);
                    }

                    string[] defaults =
                    [
                        "Custom Paints",
                        "Special",
                        "Moderator",
                        "Admin",
                        "Premium",
                    ];
                    foreach (string def in defaults)
                    {
                        if (!ExistingItemNames.Contains(def))
                        {
                            ExistingItemNames.Add(def);
                        }
                    }

                    RebuildFilterCategories();
                    FilterPaints();
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load paints in Admin View.");
                ShowNotificationSafe(
                    "Error",
                    "Failed to load custom paints.",
                    ControlAppearance.Danger
                );
            }
        }

        private void FilterPaints()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                FilteredPaints.Clear();
                IEnumerable<SkinModel> filtered = _allPaints;

                if (
                    !string.IsNullOrEmpty(SelectedFilterCategory)
                    && SelectedFilterCategory != "All"
                )
                {
                    filtered = filtered.Where(p => p.ItemName == SelectedFilterCategory);
                }

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(p =>
                        p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        || (
                            p.ItemName != null
                            && p.ItemName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        )
                    );
                }

                foreach (SkinModel paint in filtered)
                {
                    FilteredPaints.Add(paint);
                }
            });
        }

        private void RebuildFilterCategories()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                string currentSelected = SelectedFilterCategory;
                FilterCategories.Clear();
                FilterCategories.Add("All");
                foreach (string name in ExistingItemNames)
                {
                    FilterCategories.Add(name);
                }
                SelectedFilterCategory = FilterCategories.Contains(currentSelected)
                    ? currentSelected
                    : "All";
            });
        }

        private void ExecuteAddCategory(object? parameter)
        {
            string name = NewCategoryName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!ExistingItemNames.Contains(name))
                {
                    ExistingItemNames.Add(name);
                    RebuildFilterCategories();
                    Log.Information($"Added new paint category option: {name}");
                    ShowNotificationSafe(
                        "Success",
                        $"Category folder '{name}' added to choices.",
                        ControlAppearance.Success
                    );
                    NewCategoryName = string.Empty;
                }
                else
                {
                    ShowNotificationSafe(
                        "Info",
                        "This folder category choice already exists.",
                        ControlAppearance.Caution
                    );
                }
            });
        }

        private void ExecuteRemoveCategory(object? parameter)
        {
            string cat = SelectedCategoryToRemove;
            if (string.IsNullOrEmpty(cat))
            {
                return;
            }

            string[] defaults = ["Custom Paints", "Special", "Moderator", "Admin", "Premium"];
            if (defaults.Contains(cat))
            {
                ShowNotificationSafe(
                    "Access Denied",
                    $"Cannot delete system category '{cat}'.",
                    ControlAppearance.Danger
                );
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (ExistingItemNames.Contains(cat))
                {
                    int count = 0;
                    foreach (SkinModel paint in _allPaints)
                    {
                        if (paint.ItemName == cat)
                        {
                            paint.ItemName = "Custom Paints";
                            count++;
                        }
                    }

                    _ = ExistingItemNames.Remove(cat);
                    RebuildFilterCategories();
                    SelectedCategoryToRemove = string.Empty;
                    FilterPaints();

                    string countPaints = count > 1 ? "paints" : "paint";
                    Log.Information(
                        $"Removed paint category option: {cat}. Reassigned {count} {countPaints} to 'Custom Paints'."
                    );
                    ShowNotificationSafe(
                        "Success",
                        $"Deleted '{cat}'. {count} paints reassigned to 'Custom Paints'.",
                        ControlAppearance.Success
                    );
                }
            });
        }

        private void ExecuteRenameCategory(object? parameter)
        {
            string oldName = SelectedCategoryToRename;
            string newName = RenameCategoryNewName?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            {
                return;
            }

            string[] defaults = ["Custom Paints", "Special", "Moderator", "Admin", "Premium"];
            if (defaults.Contains(oldName))
            {
                ShowNotificationSafe(
                    "Access Denied",
                    $"Cannot rename system category '{oldName}'.",
                    ControlAppearance.Danger
                );
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (ExistingItemNames.Contains(newName))
                {
                    ShowNotificationSafe(
                        "Info",
                        "A folder category with that name already exists.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                if (ExistingItemNames.Contains(oldName))
                {
                    int count = 0;
                    foreach (SkinModel paint in _allPaints)
                    {
                        if (paint.ItemName == oldName)
                        {
                            paint.ItemName = newName;
                            count++;
                        }
                    }

                    int index = ExistingItemNames.IndexOf(oldName);
                    if (index != -1)
                    {
                        ExistingItemNames[index] = newName;
                    }

                    RebuildFilterCategories();
                    SelectedCategoryToRename = string.Empty;
                    RenameCategoryNewName = string.Empty;
                    FilterPaints();

                    string countPaints = count > 1 ? "paints" : "paint";
                    Log.Information(
                        $"Renamed category {oldName} to {newName}. Updated {count} {countPaints}."
                    );
                    ShowNotificationSafe(
                        "Success",
                        $"Renamed '{oldName}' to '{newName}'. {count} paints updated.",
                        ControlAppearance.Success
                    );
                }
            });
        }

        private async Task ExecuteSaveChanges(object? parameter)
        {
            try
            {
                string jsonPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.PaintsSkinsJson
                );
                Log.Information(
                    "Saving admin category reassignments back to disk: {JsonPath}",
                    jsonPath
                );

                string json = JsonSerializer.Serialize(_allPaints, _jsonOptions);
                await File.WriteAllTextAsync(jsonPath, json);

                ShowNotificationSafe(
                    "Success",
                    "Category assignments saved successfully.",
                    ControlAppearance.Success
                );
                await _mainViewModel.ReloadSkinsDataAsync();
                CloseRequested?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save paint category changes to disk.");
                ShowNotificationSafe(
                    "Error",
                    $"Failed to save changes: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
        }

        private async Task RefreshKeysAsync(bool forceRefresh = false)
        {
            try
            {
                IsLoadingKeys = true;
                List<ServerLicenseKeyItem> keys =
                    await _mainViewModel.PremiumService.FetchAllLicenseKeysAsync(forceRefresh);

                DateTime now = DateTime.UtcNow;

                List<string> expiredDeletions =
                [
                    .. _locallyDeletedKeys
                        .Where(kvp => (now - kvp.Value).TotalSeconds > 60)
                        .Select(kvp => kvp.Key),
                ];
                foreach (string exp in expiredDeletions)
                {
                    _ = _locallyDeletedKeys.Remove(exp);
                }

                List<string> expiredOverrides =
                [
                    .. _recentLocalKeyOverrides
                        .Where(kvp => (now - kvp.Value.Timestamp).TotalSeconds > 60)
                        .Select(kvp => kvp.Key),
                ];
                foreach (string exp in expiredOverrides)
                {
                    _ = _recentLocalKeyOverrides.Remove(exp);
                }

                keys = [.. keys.Where(k => !_locallyDeletedKeys.ContainsKey(k.LicenseKey))];

                foreach (ServerLicenseKeyItem serverKey in keys)
                {
                    if (
                        _recentLocalKeyOverrides.TryGetValue(
                            serverKey.LicenseKey,
                            out (
                                string Status,
                                string Hwid,
                                string Nickname,
                                DateTime Timestamp
                            ) localOverride
                        )
                    )
                    {
                        serverKey.Status = localOverride.Status;
                        serverKey.Hwid = localOverride.Hwid;
                        if (!string.IsNullOrEmpty(localOverride.Nickname))
                        {
                            serverKey.Nickname = localOverride.Nickname;
                        }
                    }
                }

                foreach (ServerLicenseKeyItem localKey in _allServerKeys)
                {
                    if (
                        !_locallyDeletedKeys.ContainsKey(localKey.LicenseKey)
                        && !keys.Any(k =>
                            k.LicenseKey.Equals(
                                localKey.LicenseKey,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    )
                    {
                        keys.Insert(0, localKey);
                    }
                }

                _allServerKeys = keys;
                CurrentKeysPage = 1;
                UpdatePagedKeys();
                UpdateProfileKey();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch license keys from server.");
                ShowNotificationSafe(
                    "Error",
                    $"Failed to fetch license keys: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
            finally
            {
                IsLoadingKeys = false;
            }
        }

        private void UpdatePagedKeys()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IEnumerable<ServerLicenseKeyItem> filtered = _allServerKeys;

                if (!string.IsNullOrWhiteSpace(LicenseSearchText))
                {
                    filtered = filtered.Where(k =>
                        k.LicenseKey.Contains(LicenseSearchText, StringComparison.OrdinalIgnoreCase)
                        || k.Nickname.Contains(
                            LicenseSearchText,
                            StringComparison.OrdinalIgnoreCase
                        )
                        || k.Hwid.Contains(LicenseSearchText, StringComparison.OrdinalIgnoreCase)
                    );
                }

                if (
                    !string.IsNullOrEmpty(SelectedLicenseStatusFilter)
                    && SelectedLicenseStatusFilter != "All"
                )
                {
                    filtered = filtered.Where(k =>
                        k.Status.Equals(
                            SelectedLicenseStatusFilter,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                }

                filtered = _sortColumn.ToLowerInvariant() switch
                {
                    "key" => _sortAscending
                        ? filtered.OrderBy(k => k.LicenseKey)
                        : filtered.OrderByDescending(k => k.LicenseKey),
                    "nickname" => _sortAscending
                        ? filtered.OrderBy(k => k.Nickname)
                        : filtered.OrderByDescending(k => k.Nickname),
                    "hwid" => _sortAscending
                        ? filtered.OrderBy(k => k.Hwid)
                        : filtered.OrderByDescending(k => k.Hwid),
                    _ => _sortAscending
                        ? filtered.OrderBy(k => k.Status)
                        : filtered.OrderByDescending(k => k.Status),
                };

                List<ServerLicenseKeyItem> list = [.. filtered];
                TotalServerKeysCount = list.Count;
                const int pageSize = 15;
                TotalKeysPages = Math.Max(
                    1,
                    (int)Math.Ceiling(TotalServerKeysCount / (double)pageSize)
                );
                if (CurrentKeysPage > TotalKeysPages)
                {
                    CurrentKeysPage = TotalKeysPages;
                }

                PagedServerKeys.Clear();
                foreach (
                    ServerLicenseKeyItem key in list.Skip((CurrentKeysPage - 1) * pageSize)
                        .Take(pageSize)
                )
                {
                    PagedServerKeys.Add(key);
                }
            });
        }

        private void ExecuteSort(object? parameter)
        {
            string col = parameter?.ToString()?.ToLowerInvariant() ?? "status";
            if (_sortColumn == col)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = col;
                _sortAscending = true;
            }

            OnPropertyChanged(nameof(SortIndicatorLicenseKey));
            OnPropertyChanged(nameof(SortIndicatorNickname));
            OnPropertyChanged(nameof(SortIndicatorHwid));
            OnPropertyChanged(nameof(SortIndicatorStatus));
            UpdatePagedKeys();
        }

        private async Task ExecuteGenerateKey(object? parameter)
        {
            string newKey = $"PRO_{Guid.NewGuid():N}"[..24].ToUpper();
            try
            {
                bool success = await _mainViewModel.PremiumService.CreateLicenseKeyAsync(newKey);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Created key: {newKey}",
                        ControlAppearance.Success
                    );
                    _allServerKeys.Insert(
                        0,
                        new ServerLicenseKeyItem
                        {
                            LicenseKey = newKey,
                            Status = "Active",
                            ExpiresAt = string.Empty,
                        }
                    );
                    _recentLocalKeyOverrides[newKey] = (
                        "Active",
                        string.Empty,
                        string.Empty,
                        DateTime.UtcNow
                    );
                    UpdatePagedKeys();
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to create key on server.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private void ExecuteExportCsv(object? parameter)
        {
            try
            {
                SaveFileDialog sfd = new()
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = "license_keys.csv",
                };
                if (sfd.ShowDialog() == true)
                {
                    StringBuilder sb = new();
                    _ = sb.AppendLine("LicenseKey,Nickname,HWID,Status,ExpiresAt");
                    foreach (ServerLicenseKeyItem k in _allServerKeys)
                    {
                        _ = sb.AppendLine(
                            $"\"{k.LicenseKey}\",\"{k.Nickname}\",\"{k.Hwid}\",\"{k.Status}\",\"{k.ExpiresAt}\""
                        );
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString());
                    ShowNotificationSafe(
                        "Success",
                        "Exported license keys to CSV.",
                        ControlAppearance.Success
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe(
                    "Error",
                    $"Export failed: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
        }

        private void ExecuteOpenAssignKeyModal(object? parameter)
        {
            if (parameter is ServerLicenseKeyItem key)
            {
                TargetAssignKeyItem = key;
                AssignNickname = !string.IsNullOrWhiteSpace(key.Nickname)
                    ? key.Nickname
                    : (
                        !string.IsNullOrWhiteSpace(_mainViewModel.Settings.UserNickname)
                            ? _mainViewModel.Settings.UserNickname
                            : Environment.UserName
                    );

                FilterAssignDevices();

                AdminDeviceItem? targetDev = null;
                if (!string.IsNullOrWhiteSpace(key.Hwid))
                {
                    targetDev = FilteredAssignDevices.FirstOrDefault(d =>
                        d.Hwid.Equals(key.Hwid, StringComparison.OrdinalIgnoreCase)
                    );
                }

                if (targetDev == null)
                {
                    string myHwid = CurrentHardwareId;
                    targetDev = FilteredAssignDevices.FirstOrDefault(d =>
                        d.Hwid.Equals(myHwid, StringComparison.OrdinalIgnoreCase)
                    );
                }

                SelectedAssignDevice = targetDev ?? FilteredAssignDevices.FirstOrDefault();
                IsAssignKeyModalVisible = true;
            }
        }

        private void FilterAssignDevices()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                FilteredAssignDevices.Clear();

                string currentHwid = CurrentHardwareId;
                bool matchesThisPc =
                    string.IsNullOrWhiteSpace(AssignSearchText)
                    || (
                        !string.IsNullOrEmpty(currentHwid)
                        && currentHwid.Contains(
                            AssignSearchText,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    || "this pc".Contains(AssignSearchText, StringComparison.OrdinalIgnoreCase);

                if (matchesThisPc && !string.IsNullOrEmpty(currentHwid))
                {
                    AdminDeviceItem thisPc =
                        _allDevices.FirstOrDefault(d =>
                            d.Hwid.Equals(currentHwid, StringComparison.OrdinalIgnoreCase)
                        ) ?? new AdminDeviceItem { Hwid = currentHwid, IpAddress = "This PC" };

                    FilteredAssignDevices.Add(thisPc);
                }

                IEnumerable<AdminDeviceItem> filtered = _allDevices;
                if (!string.IsNullOrWhiteSpace(AssignSearchText))
                {
                    filtered = filtered.Where(d =>
                        d.Hwid.Contains(AssignSearchText, StringComparison.OrdinalIgnoreCase)
                        || d.IpAddress.Contains(
                            AssignSearchText,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                }

                foreach (AdminDeviceItem d in filtered)
                {
                    if (
                        string.IsNullOrEmpty(currentHwid)
                        || !d.Hwid.Equals(currentHwid, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        FilteredAssignDevices.Add(d);
                    }
                }
            });
        }

        private async Task ExecuteConfirmAssignKey(object? parameter)
        {
            AdminDeviceItem? targetDevice = SelectedAssignDevice;
            if (targetDevice == null)
            {
                string myHwid = CurrentHardwareId;
                targetDevice =
                    FilteredAssignDevices.FirstOrDefault(d =>
                        d.Hwid.Equals(myHwid, StringComparison.OrdinalIgnoreCase)
                    ) ?? new AdminDeviceItem { Hwid = myHwid, IpAddress = "This PC" };
            }

            if (string.IsNullOrWhiteSpace(targetDevice.Hwid))
            {
                ShowNotificationSafe(
                    "Error",
                    "Please select a valid device.",
                    ControlAppearance.Danger
                );
                return;
            }

            string finalNickname = !string.IsNullOrWhiteSpace(AssignNickname)
                ? AssignNickname.Trim()
                : (
                    !string.IsNullOrWhiteSpace(_mainViewModel.Settings.UserNickname)
                        ? _mainViewModel.Settings.UserNickname
                        : Environment.UserName
                );

            string key =
                TargetAssignKeyItem?.LicenseKey ?? $"PRO_{Guid.NewGuid():N}"[..24].ToUpper();

            try
            {
                IsAssigningKey = true;
                PremiumActivationResult res =
                    await _mainViewModel.PremiumService.AssignKeyToDeviceAsync(
                        key,
                        targetDevice.Hwid,
                        finalNickname
                    );
                if (res.Success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Assigned key {key} to {finalNickname}.",
                        ControlAppearance.Success
                    );
                    IsAssignKeyModalVisible = false;

                    ServerLicenseKeyItem? existingKey = _allServerKeys.FirstOrDefault(k =>
                        k.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase)
                    );
                    if (existingKey != null)
                    {
                        existingKey.Hwid = targetDevice.Hwid;
                        existingKey.Nickname = finalNickname;
                        existingKey.Status = "Active";
                    }
                    else
                    {
                        _allServerKeys.Insert(
                            0,
                            new ServerLicenseKeyItem
                            {
                                LicenseKey = key,
                                Hwid = targetDevice.Hwid,
                                Nickname = finalNickname,
                                Status = "Active",
                            }
                        );
                    }

                    _recentLocalKeyOverrides[key] = (
                        "Active",
                        targetDevice.Hwid,
                        finalNickname,
                        DateTime.UtcNow
                    );

                    string currentHwid = HardwareFingerprintHelper.GetMachineHwid();
                    if (
                        targetDevice
                            .Hwid.Trim()
                            .Equals(currentHwid, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        _mainViewModel.Settings.PremiumLicenseKey = key;
                        _mainViewModel.Settings.UserNickname = finalNickname;
                        _mainViewModel.Settings.IsPremiumUnlocked = true;
                        if (!string.IsNullOrEmpty(res.Signature))
                        {
                            _mainViewModel.Settings.PremiumActivationSignature = res.Signature;
                        }
                        if (!string.IsNullOrEmpty(res.ExpiresAt))
                        {
                            _mainViewModel.Settings.PremiumExpiresAtUtc = res.ExpiresAt;
                        }
                        _mainViewModel.Settings.LastVerifiedUtc = DateTime.UtcNow.ToString("O");
                        _mainViewModel.SettingsService.Save(_mainViewModel.Settings);
                    }

                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        $"Failed to assign key: {res.ErrorMessage}",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
            finally
            {
                IsAssigningKey = false;
            }
        }

        private async Task ExecuteRevokeKey(object? parameter)
        {
            string key = parameter is ServerLicenseKeyItem itemParam
                ? itemParam.LicenseKey
                : (parameter?.ToString() ?? string.Empty);

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                bool success = await _mainViewModel.PremiumService.RevokeLicenseKeyAsync(key);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Key {key} revoked.",
                        ControlAppearance.Success
                    );
                    ServerLicenseKeyItem? item = _allServerKeys.FirstOrDefault(k =>
                        k.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase)
                    );
                    if (item != null)
                    {
                        item.Status = "Revoked";
                        item.Hwid = string.Empty;
                    }

                    _recentLocalKeyOverrides[key] = (
                        "Revoked",
                        string.Empty,
                        item?.Nickname ?? string.Empty,
                        DateTime.UtcNow
                    );

                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to revoke key.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteUnbindHwid(object? parameter)
        {
            string key = parameter is ServerLicenseKeyItem itemParam
                ? itemParam.LicenseKey
                : (parameter?.ToString() ?? string.Empty);

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                bool success = await _mainViewModel.PremiumService.UnbindHwidAsync(key);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"HWID unbind completed for {key}.",
                        ControlAppearance.Success
                    );
                    ServerLicenseKeyItem? item = _allServerKeys.FirstOrDefault(k =>
                        k.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase)
                    );
                    if (item != null)
                    {
                        item.Hwid = string.Empty;
                        item.Status = "Inactive";
                    }

                    _recentLocalKeyOverrides[key] = (
                        "Inactive",
                        string.Empty,
                        item?.Nickname ?? string.Empty,
                        DateTime.UtcNow
                    );

                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to unbind HWID.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteDeleteKey(object? parameter)
        {
            string key = parameter is ServerLicenseKeyItem itemParam
                ? itemParam.LicenseKey
                : (parameter?.ToString() ?? string.Empty);

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                bool success = await _mainViewModel.PremiumService.DeleteLicenseKeyAsync(key);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Key {key} deleted.",
                        ControlAppearance.Success
                    );
                    _ = _allServerKeys.RemoveAll(k =>
                        k.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase)
                    );

                    _locallyDeletedKeys[key] = DateTime.UtcNow;

                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to delete key.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task RefreshDevicesAsync(bool forceRefresh = false)
        {
            try
            {
                IsDevicesLoading = true;
                List<AdminDeviceItem> devices =
                    await _mainViewModel.DeviceBanService.GetAllDevicesAsync(forceRefresh);

                DateTime now = DateTime.UtcNow;

                List<string> expiredOverrides =
                [
                    .. _recentLocalDeviceOverrides
                        .Where(kvp => (now - kvp.Value.Timestamp).TotalSeconds > 60)
                        .Select(kvp => kvp.Key),
                ];
                foreach (string exp in expiredOverrides)
                {
                    _ = _recentLocalDeviceOverrides.Remove(exp);
                }

                foreach (AdminDeviceItem serverDev in devices)
                {
                    if (
                        _recentLocalDeviceOverrides.TryGetValue(
                            serverDev.Hwid,
                            out (
                                bool IsBanned,
                                bool IsTempBan,
                                string BanReason,
                                string BanExpiresAt,
                                DateTime Timestamp
                            ) localOverride
                        )
                    )
                    {
                        serverDev.IsBanned = localOverride.IsBanned;
                        serverDev.IsTempBan = localOverride.IsTempBan;
                        serverDev.BanReason = localOverride.BanReason;
                        serverDev.BanExpiresAt = localOverride.BanExpiresAt;
                    }
                }

                foreach (
                    KeyValuePair<
                        string,
                        (
                            bool IsBanned,
                            bool IsTempBan,
                            string BanReason,
                            string BanExpiresAt,
                            DateTime Timestamp
                        )
                    > kvp in _recentLocalDeviceOverrides
                )
                {
                    if (
                        !devices.Any(d =>
                            d.Hwid.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        devices.Insert(
                            0,
                            new AdminDeviceItem
                            {
                                Hwid = kvp.Key,
                                IpAddress = "Unknown",
                                IsBanned = kvp.Value.IsBanned,
                                IsTempBan = kvp.Value.IsTempBan,
                                BanReason = kvp.Value.BanReason,
                                BanExpiresAt = kvp.Value.BanExpiresAt,
                            }
                        );
                    }
                }

                _allDevices = devices;
                FilterDevices();
                FilterAssignDevices();
                UpdateProfileKey();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch devices.");
                ShowNotificationSafe(
                    "Error",
                    $"Failed to fetch devices: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
            finally
            {
                IsDevicesLoading = false;
            }
        }

        private void FilterDevices()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                FilteredDevices.Clear();
                IEnumerable<AdminDeviceItem> filtered = _allDevices;
                if (!string.IsNullOrWhiteSpace(DeviceFilterText))
                {
                    filtered = filtered.Where(d =>
                        d.Hwid.Contains(DeviceFilterText, StringComparison.OrdinalIgnoreCase)
                        || d.IpAddress.Contains(
                            DeviceFilterText,
                            StringComparison.OrdinalIgnoreCase
                        )
                        || d.BanReason.Contains(
                            DeviceFilterText,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                }
                foreach (AdminDeviceItem dev in filtered)
                {
                    FilteredDevices.Add(dev);
                }
            });
        }

        private async Task ExecuteBanAsync(object? parameter, int hours)
        {
            string hwid = parameter is AdminDeviceItem d
                ? d.Hwid
                : SelectedProfileDevice?.Hwid ?? parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(hwid))
            {
                return;
            }

            try
            {
                bool success = await _mainViewModel.DeviceBanService.BanDeviceAsync(
                    hwid,
                    BanReasonInput,
                    hours
                );
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Device banned successfully.",
                        ControlAppearance.Success
                    );
                    string banExp =
                        hours > 0 ? DateTime.UtcNow.AddHours(hours).ToString("o") : string.Empty;
                    AdminDeviceItem? dev = _allDevices.FirstOrDefault(x =>
                        x.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (dev != null)
                    {
                        dev.IsBanned = true;
                        dev.IsTempBan = hours > 0;
                        dev.BanReason = BanReasonInput;
                        dev.BanExpiresAt = banExp;
                    }
                    else
                    {
                        dev = new AdminDeviceItem
                        {
                            Hwid = hwid,
                            IpAddress = "Unknown",
                            IsBanned = true,
                            IsTempBan = hours > 0,
                            BanReason = BanReasonInput,
                            BanExpiresAt = banExp,
                        };
                        _allDevices.Insert(0, dev);
                    }

                    _recentLocalDeviceOverrides[hwid] = (
                        true,
                        hours > 0,
                        BanReasonInput,
                        banExp,
                        DateTime.UtcNow
                    );

                    FilterDevices();
                    FilterAssignDevices();
                    if (
                        SelectedProfileDevice != null
                        && SelectedProfileDevice.Hwid.Equals(
                            hwid,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        SelectedProfileDevice.IsBanned = true;
                        SelectedProfileDevice.IsTempBan = hours > 0;
                        SelectedProfileDevice.BanReason = BanReasonInput;
                        SelectedProfileDevice.BanExpiresAt = banExp;
                        OnPropertyChanged(nameof(SelectedProfileDevice));
                    }
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to ban device.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteUnbanDevice(object? parameter)
        {
            string hwid = parameter is AdminDeviceItem d
                ? d.Hwid
                : SelectedProfileDevice?.Hwid ?? parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(hwid))
            {
                return;
            }

            try
            {
                bool success = await _mainViewModel.DeviceBanService.UnbanDeviceAsync(hwid);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Device unbanned successfully.",
                        ControlAppearance.Success
                    );
                    AdminDeviceItem? dev = _allDevices.FirstOrDefault(x =>
                        x.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (dev != null)
                    {
                        dev.IsBanned = false;
                        dev.IsTempBan = false;
                        dev.BanReason = string.Empty;
                        dev.BanExpiresAt = string.Empty;
                    }

                    _recentLocalDeviceOverrides[hwid] = (
                        false,
                        false,
                        string.Empty,
                        string.Empty,
                        DateTime.UtcNow
                    );

                    FilterDevices();
                    FilterAssignDevices();
                    if (
                        SelectedProfileDevice != null
                        && SelectedProfileDevice.Hwid.Equals(
                            hwid,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        SelectedProfileDevice.IsBanned = false;
                        SelectedProfileDevice.IsTempBan = false;
                        SelectedProfileDevice.BanReason = string.Empty;
                        SelectedProfileDevice.BanExpiresAt = string.Empty;
                        OnPropertyChanged(nameof(SelectedProfileDevice));
                    }
                }
                else
                {
                    ShowNotificationSafe(
                        "Error",
                        "Failed to unban device.",
                        ControlAppearance.Danger
                    );
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteRevokeTrialAsync(object? parameter)
        {
            string hwid = parameter is AdminDeviceItem d
                ? d.Hwid
                : SelectedProfileDevice?.Hwid ?? parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(hwid))
            {
                ShowNotificationSafe(
                    "Warning",
                    "No device HWID selected.",
                    ControlAppearance.Caution
                );
                return;
            }

            try
            {
                (bool success, string message) =
                    await _mainViewModel.PremiumService.RevokeDeviceTrialAsync(hwid);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        "Trial revoked for device.",
                        ControlAppearance.Success
                    );
                    AdminDeviceItem? dev = _allDevices.FirstOrDefault(x =>
                        x.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (dev != null)
                    {
                        dev.IsTrial = false;
                    }

                    ServerLicenseKeyItem? trialKey = _allServerKeys.FirstOrDefault(k =>
                        k.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (trialKey != null)
                    {
                        trialKey.Status = "Revoked";
                        trialKey.Hwid = string.Empty;
                        _recentLocalKeyOverrides[trialKey.LicenseKey] = (
                            "Revoked",
                            string.Empty,
                            trialKey.Nickname,
                            DateTime.UtcNow
                        );
                    }

                    FilterDevices();
                    FilterAssignDevices();
                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe("Error", message, ControlAppearance.Danger);
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteResetTrialAsync(object? parameter)
        {
            string hwid = parameter is AdminDeviceItem d
                ? d.Hwid
                : SelectedProfileDevice?.Hwid ?? parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(hwid))
            {
                ShowNotificationSafe(
                    "Warning",
                    "No device HWID selected.",
                    ControlAppearance.Caution
                );
                return;
            }

            try
            {
                (bool success, string message) =
                    await _mainViewModel.PremiumService.ResetDeviceTrialAsync(hwid);
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        "Trial reset successfully. User may now start a new trial.",
                        ControlAppearance.Success
                    );

                    AdminDeviceItem? dev = _allDevices.FirstOrDefault(x =>
                        x.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (dev != null)
                    {
                        dev.IsTrial = false;
                    }

                    ServerLicenseKeyItem? trialKey = _allServerKeys.FirstOrDefault(k =>
                        k.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (trialKey != null)
                    {
                        trialKey.Hwid = string.Empty;
                        trialKey.Status = "Inactive";
                        _recentLocalKeyOverrides[trialKey.LicenseKey] = (
                            "Inactive",
                            string.Empty,
                            trialKey.Nickname,
                            DateTime.UtcNow
                        );
                    }

                    FilterDevices();
                    FilterAssignDevices();
                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe("Error", message, ControlAppearance.Danger);
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private async Task ExecuteExtendTrialAsync(object? parameter, int days = 3)
        {
            string hwid = parameter is AdminDeviceItem d
                ? d.Hwid
                : SelectedProfileDevice?.Hwid ?? parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(hwid))
            {
                ShowNotificationSafe(
                    "Warning",
                    "No device HWID selected.",
                    ControlAppearance.Caution
                );
                return;
            }

            try
            {
                string nickname = SelectedProfileKey?.Nickname ?? "User";
                (bool success, string message) =
                    await _mainViewModel.PremiumService.ExtendDeviceTrialAsync(
                        hwid,
                        days,
                        nickname
                    );
                if (success)
                {
                    ShowNotificationSafe(
                        "Success",
                        $"Trial activated / extended for {days} days.",
                        ControlAppearance.Success
                    );

                    AdminDeviceItem? dev = _allDevices.FirstOrDefault(x =>
                        x.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (dev != null)
                    {
                        dev.IsTrial = true;
                    }

                    string newExpIso = DateTime.UtcNow.AddDays(days).ToString("o");
                    ServerLicenseKeyItem? trialKey = _allServerKeys.FirstOrDefault(k =>
                        k.Hwid.Equals(hwid, StringComparison.OrdinalIgnoreCase)
                    );
                    if (trialKey != null)
                    {
                        trialKey.Status = "Active";
                        trialKey.ExpiresAt = newExpIso;
                        trialKey.IsTrial = true;
                        _recentLocalKeyOverrides[trialKey.LicenseKey] = (
                            "Active",
                            hwid,
                            trialKey.Nickname,
                            DateTime.UtcNow
                        );
                    }

                    FilterDevices();
                    FilterAssignDevices();
                    UpdatePagedKeys();
                    UpdateProfileKey();
                }
                else
                {
                    ShowNotificationSafe("Error", message, ControlAppearance.Danger);
                }
            }
            catch (Exception ex)
            {
                ShowNotificationSafe("Error", ex.Message, ControlAppearance.Danger);
            }
        }

        private void UpdateProfileKey()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (SelectedProfileDevice != null)
                {
                    SelectedProfileKey = _allServerKeys.FirstOrDefault(k =>
                        k.Hwid.Equals(
                            SelectedProfileDevice.Hwid,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );
                    HasProfileKey = SelectedProfileKey != null;
                }
                else
                {
                    SelectedProfileKey = null;
                    HasProfileKey = false;
                }
            });
        }

        private void RefreshLogs()
        {
            try
            {
                string logFile = App.CurrentLogFilePath;
                if (!string.IsNullOrEmpty(logFile) && File.Exists(logFile))
                {
                    using FileStream stream = new(
                        logFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    );
                    const long maxBytes = 100_000;
                    if (stream.Length > maxBytes)
                    {
                        _ = stream.Seek(-maxBytes, SeekOrigin.End);
                    }
                    using StreamReader reader = new(stream);
                    AdminLogContent = reader.ReadToEnd();
                    return;
                }
                AdminLogContent = "No log file found.";
            }
            catch (Exception ex)
            {
                AdminLogContent = $"Error reading log file: {ex.Message}";
            }
        }

        private void ShowNotificationSafe(
            string title,
            string message,
            ControlAppearance appearance
        )
        {
            _ =
                AdminNotificationRequested != null
                    ? AdminNotificationRequested.Invoke(title, message, appearance)
                    : _notificationService.ShowAsync(title, message, appearance);
        }
    }
}
