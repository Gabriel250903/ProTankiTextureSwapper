using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Microsoft.Win32;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Editors
{
    public enum PropVariant
    {
        Default,
        LcNormal,
        LcWinter,
    }

    public class PropsEditorViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly ITaraService _taraService;
        private readonly IPropComposer _propComposer;
        private byte[]? _originalTextureData;
        private byte[]? _originalMeshData;
        private byte[]? _customTextureData;
        private byte[]? _customMeshData;
        private string? _lcNormalTexturePath;
        private string? _lcNormalMeshPath;
        private string? _lcWinterTexturePath;
        private string? _lcWinterMeshPath;
        private bool _hasLcNormalVersion;
        private bool _hasLcWinterVersion;
        private PropVariant _currentInGameVariant = PropVariant.Default;
        private PropVariant _selectedVariant = PropVariant.Default;
        private string? _associatedMeshName;
        private bool _isMeshShared;
        private string _sharedMeshWarning = string.Empty;
        private List<TaraEntry>? _currentTaraEntries;
        private string? _currentTaraBase64Url;
        private readonly Dictionary<string, string> _libToPrimaryLcFolderCache = new(
            StringComparer.OrdinalIgnoreCase
        );

        private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions options = jsonSerializerOptions;

        #region Observable Properties

        private ObservableCollection<PropModel> _allProps = [];
        public ObservableCollection<PropModel> AllProps
        {
            get => _allProps;
            set => SetProperty(ref _allProps, value);
        }

        private ObservableCollection<PropModel> _filteredProps = [];
        public ObservableCollection<PropModel> FilteredProps
        {
            get => _filteredProps;
            set => SetProperty(ref _filteredProps, value);
        }

        private ObservableCollection<string> _categories = [];
        public ObservableCollection<string> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        private string _selectedCategory = "All";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyFilter();
                }
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private PropModel? _selectedProp;
        public PropModel? SelectedProp
        {
            get => _selectedProp;
            set
            {
                if (SetProperty(ref _selectedProp, value))
                {
                    OnPropertyChanged(nameof(ShowNoLcNotice));
                    OnPropertyChanged(nameof(ShowSelectPropNotice));
                    _ = LoadSelectedPropAsync();
                }
            }
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private BitmapSource? _originalPreviewImage;
        public BitmapSource? OriginalPreviewImage
        {
            get => _originalPreviewImage;
            set => SetProperty(ref _originalPreviewImage, value);
        }

        private bool _flipHorizontal;
        public bool FlipHorizontal
        {
            get => _flipHorizontal;
            set
            {
                if (SetProperty(ref _flipHorizontal, value))
                {
                    UpdatePreview();
                }
            }
        }

        private bool _flipVertical;
        public bool FlipVertical
        {
            get => _flipVertical;
            set
            {
                if (SetProperty(ref _flipVertical, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _hue;
        public double Hue
        {
            get => _hue;
            set
            {
                if (SetProperty(ref _hue, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _saturation = 100.0;
        public double Saturation
        {
            get => _saturation;
            set
            {
                if (SetProperty(ref _saturation, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _brightness;
        public double Brightness
        {
            get => _brightness;
            set
            {
                if (SetProperty(ref _brightness, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _contrast = 1.0;
        public double Contrast
        {
            get => _contrast;
            set
            {
                if (SetProperty(ref _contrast, value))
                {
                    UpdatePreview();
                }
            }
        }

        private Color _tintColor = Colors.Transparent;
        public Color TintColor
        {
            get => _tintColor;
            set
            {
                if (SetProperty(ref _tintColor, value))
                {
                    OnPropertyChanged(nameof(TintBrush));
                    OnPropertyChanged(nameof(TintHex));
                    if (TintStrength < 0.01 && value.A > 0)
                    {
                        TintStrength = 0.3;
                    }
                    UpdatePreview();
                }
            }
        }

        public SolidColorBrush TintBrush =>
            new(TintColor == Colors.Transparent ? Colors.Gray : TintColor);

        public string TintHex
        {
            get =>
                TintColor == Colors.Transparent
                    ? "None"
                    : $"#{TintColor.R:X2}{TintColor.G:X2}{TintColor.B:X2}";
            set
            {
                try
                {
                    if (
                        string.IsNullOrWhiteSpace(value)
                        || value.Equals("None", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        TintColor = Colors.Transparent;
                        return;
                    }

                    string hex = value.TrimStart('#');
                    if (hex.Length == 6)
                    {
                        byte r = Convert.ToByte(hex[..2], 16);
                        byte g = Convert.ToByte(hex[2..4], 16);
                        byte b = Convert.ToByte(hex[4..6], 16);
                        TintColor = Color.FromRgb(r, g, b);
                    }
                }
                catch { }
            }
        }

        private double _tintStrength;
        public double TintStrength
        {
            get => _tintStrength;
            set
            {
                if (SetProperty(ref _tintStrength, value))
                {
                    UpdatePreview();
                }
            }
        }

        private bool _protectEnabled;
        public bool ProtectEnabled
        {
            get => _protectEnabled;
            set
            {
                if (SetProperty(ref _protectEnabled, value))
                {
                    UpdatePreview();
                }
            }
        }

        private Color _protectColor = Colors.Yellow;
        public Color ProtectColor
        {
            get => _protectColor;
            set
            {
                if (SetProperty(ref _protectColor, value))
                {
                    OnPropertyChanged(nameof(ProtectHex));
                    if (!ProtectEnabled)
                    {
                        ProtectEnabled = true;
                    }
                    UpdatePreview();
                }
            }
        }

        public string ProtectHex
        {
            get => $"#{ProtectColor.R:X2}{ProtectColor.G:X2}{ProtectColor.B:X2}";
            set
            {
                try
                {
                    string hex = value.TrimStart('#');
                    if (hex.Length == 6)
                    {
                        byte r = Convert.ToByte(hex[..2], 16);
                        byte g = Convert.ToByte(hex[2..4], 16);
                        byte b = Convert.ToByte(hex[4..6], 16);
                        ProtectColor = Color.FromRgb(r, g, b);
                    }
                }
                catch { }
            }
        }

        private double _protectTolerance = 30.0;
        public double ProtectTolerance
        {
            get => _protectTolerance;
            set
            {
                if (SetProperty(ref _protectTolerance, value))
                {
                    UpdatePreview();
                }
            }
        }

        public IEnumerable<DecalBlendMode> AvailableBlendModes =>
            Enum.GetValues(typeof(DecalBlendMode)).Cast<DecalBlendMode>();

        private ObservableCollection<DecalOverlay> _decals = [];
        public ObservableCollection<DecalOverlay> Decals
        {
            get => _decals;
            set => SetProperty(ref _decals, value);
        }

        private DecalOverlay? _selectedDecal;
        public DecalOverlay? SelectedDecal
        {
            get => _selectedDecal;
            set => SetProperty(ref _selectedDecal, value);
        }

        private string _loadingMessage = string.Empty;
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        private bool _isWorking;
        public bool IsWorking
        {
            get => _isWorking;
            set => SetProperty(ref _isWorking, value);
        }

        private string _selectedPresetName = string.Empty;
        public string SelectedPresetName
        {
            get => _selectedPresetName;
            set => SetProperty(ref _selectedPresetName, value);
        }

        private bool _isBatching;
        public bool IsBatching
        {
            get => _isBatching;
            set
            {
                if (SetProperty(ref _isBatching, value))
                {
                    OnPropertyChanged(nameof(IsNotBatching));
                }
            }
        }

        public bool IsNotBatching => !IsBatching;

        private bool _isMassThemingEnabled;
        public bool IsMassThemingEnabled
        {
            get => _isMassThemingEnabled;
            set => SetProperty(ref _isMassThemingEnabled, value);
        }

        private double _batchProgress;
        public double BatchProgress
        {
            get => _batchProgress;
            set => SetProperty(ref _batchProgress, value);
        }

        public bool IsAdmin => AdminSession.IsAuthenticated;
        public bool IsNotAdmin => !IsAdmin;

        public bool ShowNoLcNotice => !IsAdmin && SelectedProp != null && !HasLcVersion;
        public bool ShowSelectPropNotice => !IsAdmin && SelectedProp == null;

        public bool HasLcVersion => _hasLcNormalVersion || _hasLcWinterVersion;

        public bool HasLcNormalVersion
        {
            get => _hasLcNormalVersion;
            set
            {
                if (SetProperty(ref _hasLcNormalVersion, value))
                {
                    OnPropertyChanged(nameof(HasLcVersion));
                    OnPropertyChanged(nameof(ShowNoLcNotice));
                }
            }
        }

        public bool HasLcWinterVersion
        {
            get => _hasLcWinterVersion;
            set
            {
                if (SetProperty(ref _hasLcWinterVersion, value))
                {
                    OnPropertyChanged(nameof(HasLcVersion));
                    OnPropertyChanged(nameof(ShowNoLcNotice));
                }
            }
        }

        public PropVariant SelectedVariant
        {
            get => _selectedVariant;
            set
            {
                if (SetProperty(ref _selectedVariant, value))
                {
                    OnPropertyChanged(nameof(IsDefaultSelected));
                    OnPropertyChanged(nameof(IsLcNormalSelected));
                    OnPropertyChanged(nameof(IsLcWinterSelected));
                    OnPropertyChanged(nameof(IsLcSelected));
                    OnPropertyChanged(nameof(HasPendingChanges));
                    OnVersionSelectionChanged();
                }
            }
        }

        public bool IsDefaultSelected
        {
            get => _selectedVariant == PropVariant.Default;
            set
            {
                if (value)
                {
                    SelectedVariant = PropVariant.Default;
                }
            }
        }

        public bool IsLcNormalSelected
        {
            get => _selectedVariant == PropVariant.LcNormal;
            set
            {
                if (value)
                {
                    SelectedVariant = PropVariant.LcNormal;
                }
            }
        }

        public bool IsLcSelected
        {
            get => IsLcNormalSelected;
            set => IsLcNormalSelected = value;
        }

        public bool IsLcWinterSelected
        {
            get => _selectedVariant == PropVariant.LcWinter;
            set
            {
                if (value)
                {
                    SelectedVariant = PropVariant.LcWinter;
                }
            }
        }

        public PropVariant CurrentInGameVariant
        {
            get => _currentInGameVariant;
            set
            {
                if (SetProperty(ref _currentInGameVariant, value))
                {
                    OnPropertyChanged(nameof(IsCurrentlyLc));
                    OnPropertyChanged(nameof(CurrentVersionBadgeText));
                    OnPropertyChanged(nameof(HasPendingChanges));
                }
            }
        }

        public bool IsCurrentlyLc => _currentInGameVariant != PropVariant.Default;

        public string CurrentVersionBadgeText =>
            _currentInGameVariant switch
            {
                PropVariant.LcWinter => "In-Game: LC Winter",
                PropVariant.LcNormal => "In-Game: LC Normal",
                _ => "In-Game: Default",
            };

        public string? AssociatedMeshName
        {
            get => _associatedMeshName;
            set
            {
                if (SetProperty(ref _associatedMeshName, value))
                {
                    OnPropertyChanged(nameof(HasMesh));
                    OnPropertyChanged(nameof(HasNoMesh));
                }
            }
        }

        public bool HasMesh => !string.IsNullOrEmpty(_associatedMeshName);
        public bool HasNoMesh => !HasMesh && SelectedProp != null;

        public bool IsMeshShared
        {
            get => _isMeshShared;
            set => SetProperty(ref _isMeshShared, value);
        }

        public string SharedMeshWarning
        {
            get => _sharedMeshWarning;
            set => SetProperty(ref _sharedMeshWarning, value);
        }

        public bool HasPendingChanges =>
            HasLcVersion && (_selectedVariant != _currentInGameVariant);

        private CancellationTokenSource? _batchCts;

        #endregion

        #region Commands

        public ICommand ApplyCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand ImportDecalCommand { get; }
        public ICommand RemoveDecalCommand { get; }
        public ICommand ApplyPresetCommand { get; }
        public ICommand PickTintColorCommand { get; }
        public ICommand PickProtectColorCommand { get; }

        public ICommand BatchApplyCommand { get; }
        public ICommand CancelBatchCommand { get; }
        public ICommand ApplyAllLcCommand { get; }
        public ICommand ApplyAllLcWinterCommand { get; }

        public ICommand SelectDefaultCommand { get; }
        public ICommand SelectLcCommand { get; }
        public ICommand SelectLcNormalCommand { get; }
        public ICommand SelectLcWinterCommand { get; }

        #endregion

        #region Preset Definitions

        public static readonly Dictionary<string, PropRenderOptions> Presets = new()
        {
            ["Halloween"] = new PropRenderOptions
            {
                Brightness = -60,
                Saturation = 30,
                Contrast = 1.4,
                TintColor = Color.FromRgb(50, 10, 80),
                TintStrength = 0.40,
            },
            ["Winter"] = new PropRenderOptions
            {
                Brightness = 20,
                Saturation = 20,
                Contrast = 0.9,
                TintColor = Color.FromRgb(200, 224, 255),
                TintStrength = 0.35,
            },
            ["Apocalypse"] = new PropRenderOptions
            {
                Brightness = -30,
                Saturation = 60,
                Contrast = 1.2,
                TintColor = Color.FromRgb(42, 74, 26),
                TintStrength = 0.35,
            },
            ["Night Vision"] = new PropRenderOptions
            {
                Brightness = 10,
                Saturation = 40,
                Contrast = 1.5,
                TintColor = Color.FromRgb(0, 255, 0),
                TintStrength = 0.30,
            },
        };

        public List<string> PresetNames { get; } =
        ["Halloween", "Winter", "Apocalypse", "Night Vision"];

        #endregion

        public bool IsPremiumLocked => !_mainVM.Settings.IsPremiumUnlocked;

        public PropsEditorViewModel(
            MainViewModel mainVM,
            ITaraService taraService,
            IPropComposer propComposer
        )
        {
            _mainVM = mainVM;
            _taraService = taraService;
            _propComposer = propComposer;

            _mainVM.PremiumService.PremiumStatusChanged += (s, isUnlocked) =>
            {
                _ = (
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        OnPropertyChanged(nameof(IsPremiumLocked));
                    })
                );
            };

            _mainVM.AdminVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AdminViewModel.IsAuthenticated))
                {
                    _ = (
                        Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            OnPropertyChanged(nameof(IsAdmin));
                            OnPropertyChanged(nameof(IsNotAdmin));
                            OnPropertyChanged(nameof(ShowNoLcNotice));
                            OnPropertyChanged(nameof(ShowSelectPropNotice));
                            UpdatePreview();
                            CommandManager.InvalidateRequerySuggested();
                        })
                    );
                }
            };

            ApplyCommand = new AsyncRelayCommand(
                _ => ApplyToGameAsync(),
                _ => !IsWorking && SelectedProp != null && _originalTextureData != null
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalAsync(),
                _ => !IsWorking && SelectedProp != null
            );
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters(), _ => IsAdmin);
            ImportDecalCommand = new RelayCommand(_ => ImportDecal(), _ => IsAdmin);
            RemoveDecalCommand = new RelayCommand(
                _ => RemoveSelectedDecal(),
                _ => IsAdmin && SelectedDecal != null
            );
            ApplyPresetCommand = new RelayCommand(p => ApplyPreset(p as string), _ => IsAdmin);
            PickTintColorCommand = new RelayCommand(_ => PickTintColor(), _ => IsAdmin);
            PickProtectColorCommand = new RelayCommand(_ => PickProtectColor(), _ => IsAdmin);

            BatchApplyCommand = new AsyncRelayCommand(
                _ => BatchApplyAsync(),
                _ => IsAdmin && !IsWorking && FilteredProps.Count > 0
            );
            CancelBatchCommand = new RelayCommand(_ => _batchCts?.Cancel(), _ => IsBatching);

            ApplyAllLcCommand = new AsyncRelayCommand(
                _ => ApplyAllLcPropsAsync(false),
                _ => !IsWorking
            );
            ApplyAllLcWinterCommand = new AsyncRelayCommand(
                _ => ApplyAllLcPropsAsync(true),
                _ => !IsWorking
            );

            SelectDefaultCommand = new RelayCommand(
                _ => SelectedVariant = PropVariant.Default,
                _ => !IsWorking && HasLcVersion && SelectedVariant != PropVariant.Default
            );
            SelectLcNormalCommand = new RelayCommand(
                _ => SelectedVariant = PropVariant.LcNormal,
                _ => !IsWorking && HasLcNormalVersion && SelectedVariant != PropVariant.LcNormal
            );
            SelectLcWinterCommand = new RelayCommand(
                _ => SelectedVariant = PropVariant.LcWinter,
                _ => !IsWorking && HasLcWinterVersion && SelectedVariant != PropVariant.LcWinter
            );
            SelectLcCommand = SelectLcNormalCommand;

            LoadPropsCatalog();
        }

        #region Catalog Loading

        private void LoadPropsCatalog()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "props.json");

                if (!File.Exists(jsonPath))
                {
                    Log.Warning("props.json not found at {Path}.", jsonPath);
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                
                List<PropModel>? props = JsonSerializer.Deserialize<List<PropModel>>(
                    json,
                    options
                );

                if (props == null || props.Count == 0)
                {
                    return;
                }

                AllProps = new ObservableCollection<PropModel>(props);

                List<string> cats =
                [
                    "All",
                    .. props.Select(p => p.Category).Distinct().OrderBy(c => c),
                ];
                Categories = new ObservableCollection<string>(cats);
                SelectedCategory = "All";

                ApplyFilter();

                Log.Information("Loaded {Count} props from catalog.", props.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load props catalog.");
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<PropModel> query = AllProps;

            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
            {
                query = query.Where(p =>
                    p.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                );
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || p.TextureFilename.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                );
            }

            FilteredProps = new ObservableCollection<PropModel>(query);
        }

        #endregion

        #region Prop Selection & Texture Loading

        private async Task LoadSelectedPropAsync()
        {
            if (SelectedProp == null)
            {
                _originalTextureData = null;
                _originalMeshData = null;
                _customTextureData = null;
                _customMeshData = null;
                AssociatedMeshName = null;
                IsMeshShared = false;
                SharedMeshWarning = string.Empty;
                HasLcNormalVersion = false;
                HasLcWinterVersion = false;
                CurrentInGameVariant = PropVariant.Default;
                _selectedVariant = PropVariant.Default;
                OnPropertyChanged(nameof(HasLcVersion));
                OnPropertyChanged(nameof(IsDefaultSelected));
                OnPropertyChanged(nameof(IsLcNormalSelected));
                OnPropertyChanged(nameof(IsLcWinterSelected));
                OnPropertyChanged(nameof(IsLcSelected));
                PreviewImage = null;
                OriginalPreviewImage = null;
                OnPropertyChanged(nameof(ShowNoLcNotice));
                OnPropertyChanged(nameof(ShowSelectPropNotice));
                return;
            }

            try
            {
                IsWorking = true;

                string cachePath = _mainVM.CachePath;
                string cacheFile = Path.Combine(cachePath, SelectedProp.LibraryBase64Url);

                if (!File.Exists(cacheFile))
                {
                    _originalTextureData = null;
                    _originalMeshData = null;
                    _customTextureData = null;
                    _customMeshData = null;
                    AssociatedMeshName = null;
                    IsMeshShared = false;
                    SharedMeshWarning = string.Empty;
                    HasLcNormalVersion = false;
                    HasLcWinterVersion = false;
                    CurrentInGameVariant = PropVariant.Default;
                    _selectedVariant = PropVariant.Default;
                    OnPropertyChanged(nameof(HasLcVersion));
                    OnPropertyChanged(nameof(IsDefaultSelected));
                    OnPropertyChanged(nameof(IsLcNormalSelected));
                    OnPropertyChanged(nameof(IsLcWinterSelected));
                    OnPropertyChanged(nameof(IsLcSelected));
                    OnPropertyChanged(nameof(ShowNoLcNotice));
                    OnPropertyChanged(nameof(ShowSelectPropNotice));
                    PreviewImage = null;
                    OriginalPreviewImage = null;
                    return;
                }

                string backupFile = cacheFile + ".bak";
                byte[]? backupTextureData = null;
                byte[]? backupMeshData = null;

                if (File.Exists(backupFile))
                {
                    try
                    {
                        List<TaraEntry> bakEntries = _taraService.Unpack(backupFile);
                        backupTextureData = FindMatchingTextureEntry(
                            bakEntries,
                            SelectedProp.TextureFilename
                        )?.Data;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to read backup entries from {Path}", backupFile);
                    }
                }

                if (_currentTaraBase64Url != SelectedProp.LibraryBase64Url)
                {
                    _currentTaraEntries = await Task.Run(() => _taraService.Unpack(cacheFile));
                    _currentTaraBase64Url = SelectedProp.LibraryBase64Url;
                }

                TaraEntry? textureEntry = FindMatchingTextureEntry(
                    _currentTaraEntries,
                    SelectedProp.TextureFilename
                );

                if (
                    textureEntry != null
                    && !SelectedProp.TextureFilename.Equals(
                        textureEntry.FileName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    SelectedProp.TextureFilename = textureEntry.FileName;
                }

                if (textureEntry == null && backupTextureData == null)
                {
                    _originalTextureData = null;
                    _originalMeshData = null;
                    _customTextureData = null;
                    _customMeshData = null;
                    AssociatedMeshName = null;
                    IsMeshShared = false;
                    SharedMeshWarning = string.Empty;
                    HasLcNormalVersion = false;
                    HasLcWinterVersion = false;
                    CurrentInGameVariant = PropVariant.Default;
                    _selectedVariant = PropVariant.Default;
                    OnPropertyChanged(nameof(HasLcVersion));
                    OnPropertyChanged(nameof(IsDefaultSelected));
                    OnPropertyChanged(nameof(IsLcNormalSelected));
                    OnPropertyChanged(nameof(IsLcWinterSelected));
                    OnPropertyChanged(nameof(IsLcSelected));
                    OnPropertyChanged(nameof(ShowNoLcNotice));
                    OnPropertyChanged(nameof(ShowSelectPropNotice));
                    PreviewImage = null;
                    OriginalPreviewImage = null;
                    return;
                }

                ResolveMeshForProp(SelectedProp, _currentTaraEntries);
                DetectLcVersion(SelectedProp);
                OnPropertyChanged(nameof(ShowNoLcNotice));
                OnPropertyChanged(nameof(ShowSelectPropNotice));

                if (File.Exists(backupFile) && !string.IsNullOrEmpty(AssociatedMeshName))
                {
                    try
                    {
                        List<TaraEntry> bakEntries = _taraService.Unpack(backupFile);
                        backupMeshData = bakEntries
                            .FirstOrDefault(e =>
                                e.FileName.Equals(
                                    AssociatedMeshName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            ?.Data;
                    }
                    catch { }
                }

                if (backupMeshData != null)
                {
                    _originalMeshData = backupMeshData;
                }
                else if (!string.IsNullOrEmpty(AssociatedMeshName))
                {
                    TaraEntry? meshEntry = _currentTaraEntries?.FirstOrDefault(e =>
                        e.FileName.Equals(AssociatedMeshName, StringComparison.OrdinalIgnoreCase)
                    );
                    if (meshEntry != null && !IsCurrentlyLc)
                    {
                        _originalMeshData = meshEntry.Data;
                    }
                }

                _originalTextureData =
                    backupTextureData != null && IsCurrentlyLc
                        ? backupTextureData
                        : textureEntry?.Data ?? backupTextureData;

                OriginalPreviewImage = _propComposer.ComposePreview(
                    _originalTextureData!,
                    new PropRenderOptions()
                );

                ResetFilters();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load prop texture for {Prop}.", SelectedProp?.Name);
                _originalTextureData = null;
                _originalMeshData = null;
                PreviewImage = null;
                OriginalPreviewImage = null;
                OnPropertyChanged(nameof(ShowNoLcNotice));
                OnPropertyChanged(nameof(ShowSelectPropNotice));
            }
            finally
            {
                IsWorking = false;
            }
        }

        #endregion

        #region Preview & Filters

        private void UpdatePreview()
        {
            byte[]? baseData = _customTextureData ?? _originalTextureData;
            if (baseData == null)
            {
                return;
            }

            PropRenderOptions options = BuildRenderOptions();

            _ = Task.Run(() =>
            {
                BitmapSource preview = _propComposer.ComposePreview(baseData, options);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    PreviewImage = preview;
                });
            });
        }

        private PropRenderOptions BuildRenderOptions()
        {
            return !IsAdmin
                ? new PropRenderOptions()
                : new PropRenderOptions
                {
                    Hue = Hue,
                    Saturation = Saturation,
                    Brightness = Brightness,
                    Contrast = Contrast,
                    TintColor = TintColor,
                    TintStrength = TintStrength,
                    ProtectEnabled = ProtectEnabled,
                    ProtectColor = ProtectColor,
                    ProtectTolerance = ProtectTolerance,
                    FlipHorizontal = FlipHorizontal,
                    FlipVertical = FlipVertical,
                    Decals = [.. Decals],
                };
        }

        private void ResetFilters()
        {
            _hue = 0;
            _saturation = 100;
            _brightness = 0;
            _contrast = 1.0;
            _tintColor = Colors.Transparent;
            _tintStrength = 0;
            _protectEnabled = false;
            _protectTolerance = 30.0;
            _flipHorizontal = false;
            _flipVertical = false;

            foreach (DecalOverlay decal in _decals)
            {
                decal.PropertyChanged -= OnDecalPropertyChanged;
            }
            _decals = [];
            _selectedPresetName = string.Empty;

            OnPropertyChanged(nameof(Hue));
            OnPropertyChanged(nameof(Saturation));
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(TintColor));
            OnPropertyChanged(nameof(TintStrength));
            OnPropertyChanged(nameof(TintBrush));
            OnPropertyChanged(nameof(TintHex));
            OnPropertyChanged(nameof(ProtectEnabled));
            OnPropertyChanged(nameof(ProtectTolerance));
            OnPropertyChanged(nameof(ProtectHex));
            OnPropertyChanged(nameof(FlipHorizontal));
            OnPropertyChanged(nameof(FlipVertical));
            OnPropertyChanged(nameof(Decals));
            OnPropertyChanged(nameof(SelectedPresetName));

            UpdatePreview();
        }

        #endregion

        #region Presets

        private void ApplyPreset(string? presetName)
        {
            if (
                string.IsNullOrEmpty(presetName)
                || !Presets.TryGetValue(presetName, out PropRenderOptions? preset)
            )
            {
                return;
            }

            Hue = preset.Hue;
            Saturation = preset.Saturation;
            Brightness = preset.Brightness;
            Contrast = preset.Contrast;
            TintColor = preset.TintColor;
            TintStrength = preset.TintStrength;
            SelectedPresetName = presetName;
        }

        #endregion

        #region Decals

        private void OnDecalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void ImportDecal()
        {
            OpenFileDialog dialog = new()
            {
                Title = "Import Decal / Sticker (PNG with transparency)",
                Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = false,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            DecalOverlay decal = new()
            {
                ImagePath = dialog.FileName,
                X = 128,
                Y = 128,
                Scale = 0.5,
                Rotation = 0,
                Opacity = 0.8,
            };

            decal.PropertyChanged += OnDecalPropertyChanged;
            Decals.Add(decal);
            SelectedDecal = decal;
            UpdatePreview();
        }

        private void RemoveSelectedDecal()
        {
            if (SelectedDecal != null)
            {
                SelectedDecal.PropertyChanged -= OnDecalPropertyChanged;
                _ = Decals.Remove(SelectedDecal);
                SelectedDecal = null;
                UpdatePreview();
            }
        }

        #endregion


        #region Tint Color Picker

        private void PickTintColor()
        {
            // ColorDialog dialog = new()
            // {
            //     Color = System.Drawing.Color.FromArgb(TintColor.R, TintColor.G, TintColor.B),
            // };
            //
            // if (dialog.ShowDialog() == DialogResult.OK)
            // {
            //     TintColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            //     if (TintStrength < 0.01)
            //     {
            //         TintStrength = 0.3;
            //     }
            // }
        }

        private void PickProtectColor()
        {
            // ColorDialog dialog = new()
            // {
            //     Color = System.Drawing.Color.FromArgb(
            //         ProtectColor.R,
            //         ProtectColor.G,
            //         ProtectColor.B
            //     ),
            // };
            //
            // if (dialog.ShowDialog() == DialogResult.OK)
            // {
            //     ProtectColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            //     if (!ProtectEnabled)
            //     {
            //         ProtectEnabled = true;
            //     }
            // }
        }

        #endregion

        #region Mesh Resolution & LC Swapping

        private static string? GetLcDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string lcDir = Path.Combine(baseDir, "Textures", "Props", "LC");
            if (Directory.Exists(lcDir))
            {
                return lcDir;
            }

            string altDir = Path.Combine(baseDir, "..", "..", "..", "Textures", "Props", "LC");
            return Directory.Exists(altDir) ? Path.GetFullPath(altDir) : null;
        }

        private static readonly Dictionary<string, int> FolderPriorityBonuses = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["builds"] = 100,
            ["builds_old"] = 100,
            ["landscape"] = 80,
            ["landscape_old"] = 80,
            ["complex_build_v1"] = 60,
            ["complex_build_v1_old"] = 60,
            ["land_v1"] = 50,
            ["land_v1_old"] = 50,
            ["castle"] = 40,
            ["castle_old"] = 40,
            ["elements_v1"] = 40,
            ["elements_v1_old"] = 40,
            ["defense"] = 30,
            ["defense_old"] = 30,
            ["fence"] = 30,
            ["fence_old"] = 30,
            ["kahgel"] = 30,
            ["kahgel_old"] = 30,
            ["kariz"] = 30,
            ["kariz_old"] = 30,
            ["passage"] = 30,
            ["passage_old"] = 30,
            ["yonje"] = 30,
            ["yonje_old"] = 30,
            ["zamin"] = 30,
            ["zamin_old"] = 30,
            ["buildings_v1"] = 10,
            ["buildings_v1_old"] = 10,
            ["com_build"] = 5,
            ["com_build_old"] = 5,
            ["terrain"] = 5,
            ["terrain_old"] = 5,
        };

        private static TaraEntry? FindMatchingTextureEntry(
            IEnumerable<TaraEntry>? entries,
            string targetFilename
        )
        {
            if (entries == null || string.IsNullOrEmpty(targetFilename))
            {
                return null;
            }

            string targetStem = Path.GetFileNameWithoutExtension(targetFilename);
            string cleanStem = targetStem.Replace(
                "_halloween",
                "",
                StringComparison.OrdinalIgnoreCase
            );

            TaraEntry? entry = entries.FirstOrDefault(e =>
                e.FileName.Equals(targetFilename, StringComparison.OrdinalIgnoreCase)
            );
            if (entry != null)
            {
                return entry;
            }

            entry = entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FileName);
                if (
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                )
                {
                    string stem = Path.GetFileNameWithoutExtension(e.FileName);
                    return stem.Equals(targetStem, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            });
            if (entry != null)
            {
                return entry;
            }

            entry = entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FileName);
                if (
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                )
                {
                    string stem = Path.GetFileNameWithoutExtension(e.FileName);
                    return stem.Equals(cleanStem, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            });
            if (entry != null)
            {
                return entry;
            }

            if (
                MeshStemAliases.TryGetValue(targetStem, out string? alias)
                || MeshStemAliases.TryGetValue(cleanStem, out alias)
            )
            {
                entry = entries.FirstOrDefault(e =>
                {
                    string ext = Path.GetExtension(e.FileName);
                    if (
                        ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        string stem = Path.GetFileNameWithoutExtension(e.FileName);
                        return stem.Equals(alias, StringComparison.OrdinalIgnoreCase)
                            || (
                                MeshStemAliases.TryGetValue(stem, out string? eAlias)
                                && eAlias.Equals(alias, StringComparison.OrdinalIgnoreCase)
                            );
                    }
                    return false;
                });
                if (entry != null)
                {
                    return entry;
                }
            }

            entry = entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FileName);
                if (
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                )
                {
                    string stem = Path.GetFileNameWithoutExtension(e.FileName);
                    if (MeshStemAliases.TryGetValue(stem, out string? eAlias))
                    {
                        return eAlias.Equals(targetStem, StringComparison.OrdinalIgnoreCase)
                            || eAlias.Equals(cleanStem, StringComparison.OrdinalIgnoreCase);
                    }
                }
                return false;
            });

            return entry;
        }

        private static TaraEntry? FindMatchingMeshEntry(
            IEnumerable<TaraEntry>? entries,
            string? targetMeshName
        )
        {
            if (entries == null || string.IsNullOrEmpty(targetMeshName))
            {
                return null;
            }

            string targetStem = Path.GetFileNameWithoutExtension(targetMeshName);
            string cleanStem = targetStem.Replace(
                "_halloween",
                "",
                StringComparison.OrdinalIgnoreCase
            );

            TaraEntry? entry = entries.FirstOrDefault(e =>
                e.FileName.Equals(targetMeshName, StringComparison.OrdinalIgnoreCase)
            );
            if (entry != null)
            {
                return entry;
            }

            entry = entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FileName);
                if (ext.Equals(".3ds", StringComparison.OrdinalIgnoreCase))
                {
                    string stem = Path.GetFileNameWithoutExtension(e.FileName);
                    return stem.Equals(targetStem, StringComparison.OrdinalIgnoreCase)
                        || stem.Equals(cleanStem, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            });
            if (entry != null)
            {
                return entry;
            }

            if (
                MeshStemAliases.TryGetValue(targetStem, out string? alias)
                || MeshStemAliases.TryGetValue(cleanStem, out alias)
            )
            {
                entry = entries.FirstOrDefault(e =>
                {
                    string ext = Path.GetExtension(e.FileName);
                    if (ext.Equals(".3ds", StringComparison.OrdinalIgnoreCase))
                    {
                        string stem = Path.GetFileNameWithoutExtension(e.FileName);
                        return stem.Equals(alias, StringComparison.OrdinalIgnoreCase)
                            || (
                                MeshStemAliases.TryGetValue(stem, out string? eAlias)
                                && eAlias.Equals(alias, StringComparison.OrdinalIgnoreCase)
                            );
                    }
                    return false;
                });
                if (entry != null)
                {
                    return entry;
                }
            }

            entry = entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FileName);
                if (ext.Equals(".3ds", StringComparison.OrdinalIgnoreCase))
                {
                    string stem = Path.GetFileNameWithoutExtension(e.FileName);
                    if (MeshStemAliases.TryGetValue(stem, out string? eAlias))
                    {
                        return eAlias.Equals(targetStem, StringComparison.OrdinalIgnoreCase)
                            || eAlias.Equals(cleanStem, StringComparison.OrdinalIgnoreCase);
                    }
                }
                return false;
            });

            return entry;
        }

        private string? GetPrimaryLcFolderForLibrary(string libraryBase64Url, string lcDir)
        {
            if (string.IsNullOrEmpty(libraryBase64Url) || string.IsNullOrEmpty(lcDir))
            {
                return null;
            }

            if (_libToPrimaryLcFolderCache.TryGetValue(libraryBase64Url, out string? cached))
            {
                return cached;
            }

            List<PropModel> libProps =
                _allProps?.Where(p => p.LibraryBase64Url == libraryBase64Url).ToList() ?? [];
            if (libProps.Count == 0)
            {
                return null;
            }

            HashSet<string> texStems = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> meshStems = new(StringComparer.OrdinalIgnoreCase);

            foreach (PropModel p in libProps)
            {
                string tStem = Path.GetFileNameWithoutExtension(p.TextureFilename)
                    .ToLowerInvariant();
                _ = texStems.Add(tStem);
                string cleanTStem = tStem.Replace("_halloween", "");
                _ = texStems.Add(cleanTStem);

                if (MeshStemAliases.TryGetValue(tStem, out string? alias))
                {
                    _ = texStems.Add(alias);
                }
                if (MeshStemAliases.TryGetValue(cleanTStem, out string? cleanAlias))
                {
                    _ = texStems.Add(cleanAlias);
                }
                foreach (KeyValuePair<string, string> kvp in MeshStemAliases)
                {
                    if (
                        kvp.Value.Equals(tStem, StringComparison.OrdinalIgnoreCase)
                        || kvp.Value.Equals(cleanTStem, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        _ = texStems.Add(kvp.Key);
                    }
                }
                if (!string.IsNullOrEmpty(p.MeshFilename))
                {
                    string mStem = Path.GetFileNameWithoutExtension(p.MeshFilename)
                        .ToLowerInvariant();
                    _ = meshStems.Add(mStem);
                    if (MeshStemAliases.TryGetValue(mStem, out string? mAlias))
                    {
                        _ = meshStems.Add(mAlias);
                    }
                }
            }

            string? bestFolder = null;
            int maxScore = -1;

            foreach (string subDir in Directory.EnumerateDirectories(lcDir))
            {
                string folderName = Path.GetFileName(subDir);
                if (folderName.Contains("winter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int matchCount = 0;
                int score = 0;
                foreach (string file in Directory.EnumerateFiles(subDir))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string stem = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (ext is ".jpg" or ".png")
                    {
                        if (texStems.Contains(stem))
                        {
                            score += 10;
                            matchCount++;
                        }
                        else if (stem.Contains('_'))
                        {
                            string basePart = stem[..stem.LastIndexOf('_')];
                            if (texStems.Contains(basePart))
                            {
                                score += 5;
                            }
                        }
                    }
                    else if (ext == ".3ds")
                    {
                        if (meshStems.Contains(stem) || texStems.Contains(stem))
                        {
                            score += 8;
                            matchCount++;
                        }
                    }
                }

                if (matchCount > 0)
                {
                    string winterPair = Path.Combine(lcDir, $"{folderName}_winter");
                    string winterPairOld = Path.Combine(lcDir, $"{folderName}_winter_old");
                    string winterPairAlt = Path.Combine(
                        lcDir,
                        folderName.Replace("_old", "_winter_old")
                    );
                    if (
                        Directory.Exists(winterPair)
                        || Directory.Exists(winterPairOld)
                        || Directory.Exists(winterPairAlt)
                    )
                    {
                        score += 20;
                    }

                    if (FolderPriorityBonuses.TryGetValue(folderName, out int priorityBonus))
                    {
                        score += priorityBonus;
                    }

                    if (score > maxScore)
                    {
                        maxScore = score;
                        bestFolder = folderName;
                    }
                }
            }

            if (bestFolder != null)
            {
                _libToPrimaryLcFolderCache[libraryBase64Url] = bestFolder;
            }

            return bestFolder;
        }

        private static string? FindTextureInDirs(string texFileName, IEnumerable<string> searchDirs)
        {
            string texStem = Path.GetFileNameWithoutExtension(texFileName);
            string texExt = Path.GetExtension(texFileName);
            if (string.IsNullOrEmpty(texExt))
            {
                texExt = ".jpg";
            }

            List<string> candidateNames =
            [
                texFileName,
                $"{texStem}{texExt}",
                $"{texStem}.jpg",
                $"{texStem}.png",
                $"{texStem.ToLowerInvariant()}{texExt.ToLowerInvariant()}",
                $"{texStem.ToLowerInvariant()}.jpg",
                $"{texStem.ToLowerInvariant()}.png",
            ];

            foreach (string dir in searchDirs)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (string cand in candidateNames)
                {
                    string full = Path.Combine(dir, cand);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
            }

            return null;
        }

        private static readonly Dictionary<string, string> MeshStemAliases = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["cornerne"] = "corner",
            ["cornernw"] = "corner",
            ["cornerse"] = "corner",
            ["cornersw"] = "corner",
            ["doorn"] = "door",
            ["doors"] = "door",
            ["ende"] = "end",
            ["endn"] = "end",
            ["ends"] = "end",
            ["endw"] = "end",
            ["outwdne"] = "outwd",
            ["outwdnw"] = "outwd",
            ["outwdse"] = "outwd",
            ["outwdsw"] = "outwd",
            ["walln"] = "wall",
            ["walls"] = "wall",
            ["wallw"] = "wall",
            ["walle"] = "wall",
            ["windown"] = "window",
            ["windows"] = "window",
            ["windoww"] = "window",
            ["windowe"] = "window",
            ["balaroe"] = "balaro",
            ["balaron"] = "balaro",
            ["balaros"] = "balaro",
            ["balarow"] = "balaro",
            ["brokene"] = "broken",
            ["brokenn"] = "broken",
            ["brokens"] = "broken",
            ["brokenw"] = "broken",
            ["carpetns"] = "carpet",
            ["carpetwe"] = "carpet",
            ["darvazen"] = "darvaze",
            ["darvazes"] = "darvaze",
            ["gonbadmje"] = "gonbadmj",
            ["gonbadmjn"] = "gonbadmj",
            ["gonbadmjs"] = "gonbadmj",
            ["gonbadmjw"] = "gonbadmj",
            ["khg_cne"] = "corner",
            ["khg_cnw"] = "corner",
            ["khg_cse"] = "corner",
            ["khg_csw"] = "corner",
            ["khg_ee"] = "end",
            ["khg_en"] = "end",
            ["khg_es"] = "end",
            ["khg_ew"] = "end",
            ["khg_we"] = "wall",
            ["khg_wn"] = "wall",
            ["khg_ws"] = "wall",
            ["khg_ww"] = "wall",
            ["khg_wde"] = "window",
            ["khg_wdn"] = "window",
            ["khg_wds"] = "window",
            ["khg_wdw"] = "window",
            ["minarn"] = "minar",
            ["minars"] = "minar",
            ["dare"] = "dar",
            ["darn"] = "dar",
            ["dars"] = "dar",
            ["darw"] = "dar",
            ["dargahe"] = "dargah",
            ["dargaht"] = "dargah",
            ["dargahn"] = "dargah",
            ["dargahs"] = "dargah",
            ["dargahw"] = "dargah",
            ["hs_par10"] = "hs_part10",
            ["hs_par12"] = "hs_part12",
            ["vilhou_1"] = "vilhou_1",
            ["vilhou_2"] = "vilhou_2",
            ["vilhou_3"] = "vilhou_3",
            ["vilhou_4"] = "vilhou_4",
            ["vilhou1"] = "vilhou_1",
            ["vilhou2"] = "vilhou_2",
            ["vilhou3"] = "vilhou_3",
            ["vilhou4"] = "vilhou_4",
            ["vilhou1_halloween"] = "vilhou_1",
            ["vilhou2_halloween"] = "vilhou_2",
            ["vilhou3_halloween"] = "vilhou_3",
            ["vilhou4_halloween"] = "vilhou_4",
            ["smhouse2"] = "smhouse2",
            ["smhouse3"] = "smhouse3",
            ["smhouse4"] = "smhouse4",
            ["smhouse5"] = "smhouse5",
            ["watch_to"] = "watch_to",
            ["tower"] = "tower",
            ["fab_tow"] = "fab_tow",
            ["fab_tow2"] = "fab_tow2",
            ["gasoline"] = "gasoline",
            ["contain"] = "contain",
            ["contain2"] = "contain",
            ["tunnel1"] = "tunnel_1",
            ["tunnel2"] = "tunnel_2",
            ["broktank"] = "brok_tank",
            ["bilboard"] = "bilboard",
            ["billboard"] = "bilboard",
            ["st_br1"] = "st_br01",
            ["st_br2"] = "st_br02",
            ["sm_rck1"] = "sm_rock1",
            ["sm_rck2"] = "sm_rock2",
            ["sm_rck3"] = "sm_rock1",
            ["sm_rck4"] = "sm_rock2",
            ["bg_rock1"] = "bg_rock04",
            ["bg_rock2"] = "bg_rock04",
            ["bg_rock3"] = "bg_rock04",
            ["bg_rock4"] = "bg_rock04",
            ["up_rock1"] = "up_rock",
            ["up_rock2"] = "up_rock2",
            ["tile2sd"] = "tile2_1",
            ["nubu_2_halloween"] = "nubu_2",
            ["nubu_8_halloween"] = "nubu_8",
            ["nubu_9_halloween"] = "nubu_9",
            ["nubu_12_halloween"] = "nubu_12",
        };

        private static List<string> BuildCandidateMeshNames(
            string? associatedMesh,
            string? propMesh,
            string texFilename,
            string propName
        )
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(associatedMesh))
            {
                _ = names.Add(associatedMesh);
                _ = names.Add($"{Path.GetFileNameWithoutExtension(associatedMesh)}.3ds");
            }
            if (!string.IsNullOrEmpty(propMesh))
            {
                _ = names.Add(propMesh);
                _ = names.Add($"{Path.GetFileNameWithoutExtension(propMesh)}.3ds");
            }
            string texStem = Path.GetFileNameWithoutExtension(texFilename);
            if (!string.IsNullOrEmpty(texStem))
            {
                _ = names.Add($"{texStem}.3ds");
                string cleanTexStem = texStem.Replace(
                    "_halloween",
                    "",
                    StringComparison.OrdinalIgnoreCase
                );
                _ = names.Add($"{cleanTexStem}.3ds");

                if (MeshStemAliases.TryGetValue(texStem, out string? alias))
                {
                    _ = names.Add($"{alias}.3ds");
                }
                if (MeshStemAliases.TryGetValue(cleanTexStem, out string? cleanAlias))
                {
                    _ = names.Add($"{cleanAlias}.3ds");
                }
            }
            string cleanPropName = propName.Replace(" ", "");
            if (!string.IsNullOrEmpty(cleanPropName))
            {
                _ = names.Add($"{cleanPropName}.3ds");
                _ = names.Add($"{propName}.3ds");
                string cleanNoH = cleanPropName.Replace(
                    "Halloween",
                    "",
                    StringComparison.OrdinalIgnoreCase
                );
                _ = names.Add($"{cleanNoH}.3ds");
            }
            return [.. names];
        }

        private string? ResolveMeshFilenameForProp(PropModel prop, List<TaraEntry>? entries)
        {
            if (!string.IsNullOrEmpty(prop.MeshFilename))
            {
                TaraEntry? exactEntry = FindMatchingMeshEntry(entries, prop.MeshFilename);
                if (exactEntry != null)
                {
                    prop.MeshFilename = exactEntry.FileName;
                }
                return prop.MeshFilename;
            }

            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            string? resolvedMesh = null;
            bool isSprite = false;

            TaraEntry? xmlEntry = entries.FirstOrDefault(e =>
                e.FileName.Equals("library.xml", StringComparison.OrdinalIgnoreCase)
            );

            if (xmlEntry != null)
            {
                try
                {
                    XmlDocument xmlDoc = new();
                    string xmlStr = Encoding.UTF8.GetString(xmlEntry.Data);
                    xmlDoc.LoadXml(xmlStr);

                    string pStem = Path.GetFileNameWithoutExtension(prop.TextureFilename);
                    string cleanPStem = pStem.Replace(
                        "_halloween",
                        "",
                        StringComparison.OrdinalIgnoreCase
                    );

                    foreach (XmlNode tNode in xmlDoc.SelectNodes("//texture")!)
                    {
                        string? dMap = tNode.Attributes?["diffuse-map"]?.Value;
                        if (!string.IsNullOrEmpty(dMap))
                        {
                            string dStem = Path.GetFileNameWithoutExtension(dMap);
                            if (
                                dMap.Equals(
                                    prop.TextureFilename,
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || dStem.Equals(pStem, StringComparison.OrdinalIgnoreCase)
                                || dStem.Equals(cleanPStem, StringComparison.OrdinalIgnoreCase)
                                || (
                                    MeshStemAliases.TryGetValue(pStem, out string? pAlias)
                                    && dStem.Equals(pAlias, StringComparison.OrdinalIgnoreCase)
                                )
                                || (
                                    MeshStemAliases.TryGetValue(dStem, out string? dAlias)
                                    && dAlias.Equals(pStem, StringComparison.OrdinalIgnoreCase)
                                )
                            )
                            {
                                XmlNode? meshAncestor = tNode.SelectSingleNode("ancestor::mesh");
                                if (meshAncestor != null)
                                {
                                    resolvedMesh = meshAncestor.Attributes?["file"]?.Value;
                                    break;
                                }
                                if (
                                    tNode.ParentNode != null
                                    && tNode.ParentNode.Name.Equals(
                                        "mesh",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    resolvedMesh = tNode.ParentNode.Attributes?["file"]?.Value;
                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(resolvedMesh))
                    {
                        foreach (XmlNode pNode in xmlDoc.SelectNodes("//prop")!)
                        {
                            string? pName = pNode.Attributes?["name"]?.Value;
                            if (
                                string.Equals(pName, prop.Name, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(
                                    pName?.Replace(" ", ""),
                                    prop.Name.Replace(" ", ""),
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || string.Equals(
                                    pName?.Replace(" ", ""),
                                    prop.Name.Replace(" ", "")
                                        .Replace(
                                            "Halloween",
                                            "",
                                            StringComparison.OrdinalIgnoreCase
                                        ),
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                XmlNode? spriteNode =
                                    pNode.SelectSingleNode(".//sprite")
                                    ?? pNode.SelectSingleNode("sprite");
                                if (spriteNode != null)
                                {
                                    isSprite = true;
                                    break;
                                }

                                XmlNode? meshNode =
                                    pNode.SelectSingleNode(".//mesh")
                                    ?? pNode.SelectSingleNode("mesh");
                                if (meshNode != null)
                                {
                                    resolvedMesh = meshNode.Attributes?["file"]?.Value;
                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(resolvedMesh) && !isSprite)
                    {
                        foreach (XmlNode sNode in xmlDoc.SelectNodes("//sprite")!)
                        {
                            string? sFile = sNode.Attributes?["file"]?.Value;
                            if (!string.IsNullOrEmpty(sFile))
                            {
                                string sStem = Path.GetFileNameWithoutExtension(sFile);
                                if (
                                    sFile.Equals(
                                        prop.TextureFilename,
                                        StringComparison.OrdinalIgnoreCase
                                    ) || sStem.Equals(pStem, StringComparison.OrdinalIgnoreCase)
                                )
                                {
                                    isSprite = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to parse library.xml for mesh resolution.");
                }
            }

            if (string.IsNullOrEmpty(resolvedMesh) && !isSprite)
            {
                List<string> candidates = BuildCandidateMeshNames(
                    null,
                    prop.MeshFilename,
                    prop.TextureFilename,
                    prop.Name
                );
                foreach (string cand in candidates)
                {
                    TaraEntry? match = FindMatchingMeshEntry(entries, cand);
                    if (match != null)
                    {
                        resolvedMesh = match.FileName;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(resolvedMesh))
            {
                TaraEntry? entry = FindMatchingMeshEntry(entries, resolvedMesh);
                string finalName = entry != null ? entry.FileName : resolvedMesh;
                prop.MeshFilename = finalName;
                return finalName;
            }

            return null;
        }

        private void ResolveMeshForProp(PropModel prop, List<TaraEntry>? entries)
        {
            string? resolvedMesh = ResolveMeshFilenameForProp(prop, entries);
            AssociatedMeshName = resolvedMesh;
            IsMeshShared = false;
            SharedMeshWarning = string.Empty;

            if (string.IsNullOrEmpty(resolvedMesh) || entries == null || entries.Count == 0)
            {
                return;
            }

            TaraEntry? xmlEntry = entries.FirstOrDefault(e =>
                e.FileName.Equals("library.xml", StringComparison.OrdinalIgnoreCase)
            );

            XmlDocument? xmlDoc = null;
            if (xmlEntry != null)
            {
                try
                {
                    xmlDoc = new XmlDocument();
                    string xmlStr = Encoding.UTF8.GetString(xmlEntry.Data);
                    xmlDoc.LoadXml(xmlStr);
                }
                catch { }
            }

            List<string> sharingNames = [];
            if (xmlDoc != null)
            {
                try
                {
                    foreach (XmlNode pNode in xmlDoc.SelectNodes("//prop")!)
                    {
                        XmlNode? mNode = pNode.SelectSingleNode("mesh");
                        string? mFile = mNode?.Attributes?["file"]?.Value;
                        if (
                            !string.IsNullOrEmpty(mFile)
                            && mFile.Equals(AssociatedMeshName, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            string? pName = pNode.Attributes?["name"]?.Value;
                            if (
                                !string.IsNullOrEmpty(pName)
                                && !sharingNames.Contains(pName, StringComparer.OrdinalIgnoreCase)
                            )
                            {
                                sharingNames.Add(pName);
                            }
                        }
                    }
                }
                catch { }
            }

            if (
                _allProps != null
                && _allProps.Count > 0
                && !string.IsNullOrEmpty(AssociatedMeshName)
            )
            {
                string meshBase =
                    Path.GetFileNameWithoutExtension(AssociatedMeshName) ?? string.Empty;
                foreach (PropModel p in _allProps)
                {
                    if (p.LibraryBase64Url == prop.LibraryBase64Url && p != prop)
                    {
                        string pTexBase =
                            Path.GetFileNameWithoutExtension(p.TextureFilename) ?? string.Empty;
                        if (
                            p.MeshFilename?.Equals(
                                AssociatedMeshName,
                                StringComparison.OrdinalIgnoreCase
                            ) == true
                            || (
                                !string.IsNullOrEmpty(meshBase)
                                && !string.IsNullOrEmpty(pTexBase)
                                && (
                                    pTexBase.StartsWith(
                                        meshBase,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || meshBase.StartsWith(
                                        pTexBase,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            )
                        )
                        {
                            if (!sharingNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                            {
                                sharingNames.Add(p.Name);
                            }
                        }
                    }
                }
            }

            List<string> others =
            [
                .. sharingNames.Where(n =>
                    !n.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)
                ),
            ];
            if (others.Count > 0)
            {
                IsMeshShared = true;
                SharedMeshWarning =
                    $"Notice: This 3D mesh ({AssociatedMeshName}) is shared with other props in this library ({string.Join(", ", others.Take(5))}{(others.Count > 5 ? $" and {others.Count - 5} more" : "")}). Swapping it will update the 3D model for all of them.";
            }
        }

        private void DetectLcVersion(PropModel prop)
        {
            _lcNormalTexturePath = null;
            _lcNormalMeshPath = null;
            _lcWinterTexturePath = null;
            _lcWinterMeshPath = null;
            HasLcNormalVersion = false;
            HasLcWinterVersion = false;
            CurrentInGameVariant = PropVariant.Default;
            _selectedVariant = PropVariant.Default;

            if (
                string.IsNullOrEmpty(AssociatedMeshName) && !string.IsNullOrEmpty(prop.MeshFilename)
            )
            {
                AssociatedMeshName = prop.MeshFilename;
            }

            string? lcDir = GetLcDirectory();
            if (lcDir == null || !Directory.Exists(lcDir))
            {
                OnPropertyChanged(nameof(HasLcVersion));
                OnPropertyChanged(nameof(IsDefaultSelected));
                OnPropertyChanged(nameof(IsLcNormalSelected));
                OnPropertyChanged(nameof(IsLcWinterSelected));
                OnPropertyChanged(nameof(IsLcSelected));
                return;
            }

            string texBase = Path.GetFileNameWithoutExtension(prop.TextureFilename);
            string cleanTexBase = texBase.Replace(
                "_halloween",
                "",
                StringComparison.OrdinalIgnoreCase
            );
            List<string> candidateTexNames =
            [
                prop.TextureFilename,
                $"{texBase}.jpg",
                $"{texBase}.png",
                $"{cleanTexBase}.jpg",
                $"{cleanTexBase}.png",
                $"{prop.Name}.jpg",
                $"{prop.Name}.png",
                $"{prop.Name.Replace(" ", "")}.jpg",
                $"{prop.Name.Replace(" ", "")}.png",
                $"{prop.Name.Replace(" ", "").Replace("Halloween", "", StringComparison.OrdinalIgnoreCase)}.jpg",
                $"{prop.Name.Replace(" ", "").Replace("Halloween", "", StringComparison.OrdinalIgnoreCase)}.png",
            ];

            List<string> candidateMeshNames = BuildCandidateMeshNames(
                AssociatedMeshName,
                prop.MeshFilename,
                prop.TextureFilename,
                prop.Name
            );

            try
            {
                string? primaryFolder = GetPrimaryLcFolderForLibrary(prop.LibraryBase64Url, lcDir);

                List<string> normalDirs = [];
                List<string> winterDirs = [];

                if (!string.IsNullOrEmpty(primaryFolder))
                {
                    string pNorm = Path.Combine(lcDir, primaryFolder);
                    if (Directory.Exists(pNorm))
                    {
                        normalDirs.Add(pNorm);
                    }

                    string pWint = Path.Combine(lcDir, $"{primaryFolder}_winter");
                    string pWintOld = Path.Combine(lcDir, $"{primaryFolder}_winter_old");
                    string pWintAlt = Path.Combine(
                        lcDir,
                        primaryFolder.Replace("_old", "_winter_old")
                    );
                    if (Directory.Exists(pWint))
                    {
                        winterDirs.Add(pWint);
                    }
                    else if (Directory.Exists(pWintOld))
                    {
                        winterDirs.Add(pWintOld);
                    }
                    else if (Directory.Exists(pWintAlt))
                    {
                        winterDirs.Add(pWintAlt);
                    }
                }

                List<string> allDirs =
                [
                    .. Directory
                        .EnumerateDirectories(lcDir)
                        .OrderByDescending(d =>
                        {
                            string fn = Path.GetFileName(d)
                                .Replace("_winter_old", "_old")
                                .Replace("_winter", "");
                            return FolderPriorityBonuses.TryGetValue(fn, out int b) ? b : 0;
                        }),
                ];

                foreach (string subDir in allDirs)
                {
                    if (
                        Path.GetFileName(subDir)
                            .Contains("winter", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        if (!winterDirs.Contains(subDir, StringComparer.OrdinalIgnoreCase))
                        {
                            winterDirs.Add(subDir);
                        }
                    }
                    else
                    {
                        if (!normalDirs.Contains(subDir, StringComparer.OrdinalIgnoreCase))
                        {
                            normalDirs.Add(subDir);
                        }
                    }
                }

                foreach (string dir in normalDirs)
                {
                    foreach (string name in candidateTexNames)
                    {
                        string direct = Path.Combine(dir, name);
                        if (File.Exists(direct))
                        {
                            _lcNormalTexturePath = direct;
                            break;
                        }
                    }
                    if (_lcNormalTexturePath != null)
                    {
                        break;
                    }
                }

                foreach (string dir in winterDirs)
                {
                    foreach (string name in candidateTexNames)
                    {
                        string direct = Path.Combine(dir, name);
                        if (File.Exists(direct))
                        {
                            _lcWinterTexturePath = direct;
                            break;
                        }
                    }
                    if (_lcWinterTexturePath != null)
                    {
                        break;
                    }
                }

                if (candidateMeshNames.Count > 0)
                {
                    foreach (string dir in normalDirs)
                    {
                        foreach (string name in candidateMeshNames)
                        {
                            string direct = Path.Combine(dir, name);
                            if (File.Exists(direct))
                            {
                                _lcNormalMeshPath = direct;
                                break;
                            }
                        }
                        if (_lcNormalMeshPath != null)
                        {
                            break;
                        }
                    }

                    foreach (string dir in winterDirs)
                    {
                        foreach (string name in candidateMeshNames)
                        {
                            string direct = Path.Combine(dir, name);
                            if (File.Exists(direct))
                            {
                                _lcWinterMeshPath = direct;
                                break;
                            }
                        }
                        if (_lcWinterMeshPath != null)
                        {
                            break;
                        }
                    }

                    if (_lcWinterMeshPath == null && _lcNormalMeshPath != null)
                    {
                        _lcWinterMeshPath = _lcNormalMeshPath;
                    }

                    if (string.IsNullOrEmpty(AssociatedMeshName))
                    {
                        string? foundMeshPath = _lcNormalMeshPath ?? _lcWinterMeshPath;
                        if (foundMeshPath != null)
                        {
                            AssociatedMeshName = Path.GetFileName(foundMeshPath);
                            prop.MeshFilename = AssociatedMeshName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error scanning LC directory for prop {Prop}", prop.Name);
            }

            HasLcNormalVersion = _lcNormalTexturePath != null || _lcNormalMeshPath != null;
            HasLcWinterVersion = _lcWinterTexturePath != null || _lcWinterMeshPath != null;
            OnPropertyChanged(nameof(HasLcVersion));

            PropVariant inGame = PropVariant.Default;
            if (_currentTaraEntries != null)
            {
                if (
                    HasLcWinterVersion
                    && MatchesVariant(
                        _lcWinterTexturePath,
                        _lcWinterMeshPath ?? _lcNormalMeshPath,
                        prop
                    )
                )
                {
                    inGame = PropVariant.LcWinter;
                }
                else if (
                    HasLcNormalVersion
                    && MatchesVariant(_lcNormalTexturePath, _lcNormalMeshPath, prop)
                )
                {
                    inGame = PropVariant.LcNormal;
                }
            }

            CurrentInGameVariant = inGame;
            _selectedVariant = inGame;

            OnPropertyChanged(nameof(IsDefaultSelected));
            OnPropertyChanged(nameof(IsLcNormalSelected));
            OnPropertyChanged(nameof(IsLcWinterSelected));
            OnPropertyChanged(nameof(IsLcSelected));

            if (_selectedVariant == PropVariant.LcWinter)
            {
                if (_lcWinterTexturePath != null)
                {
                    _customTextureData = File.ReadAllBytes(_lcWinterTexturePath);
                }

                string? meshP = _lcWinterMeshPath ?? _lcNormalMeshPath;
                if (meshP != null)
                {
                    _customMeshData = File.ReadAllBytes(meshP);
                }
            }
            else if (_selectedVariant == PropVariant.LcNormal)
            {
                if (_lcNormalTexturePath != null)
                {
                    _customTextureData = File.ReadAllBytes(_lcNormalTexturePath);
                }

                if (_lcNormalMeshPath != null)
                {
                    _customMeshData = File.ReadAllBytes(_lcNormalMeshPath);
                }
            }
            else
            {
                _customTextureData = null;
                _customMeshData = null;
            }
        }

        private bool MatchesVariant(string? texPath, string? meshPath, PropModel prop)
        {
            if (_currentTaraEntries == null)
            {
                return false;
            }

            if (meshPath != null && !string.IsNullOrEmpty(AssociatedMeshName))
            {
                TaraEntry? meshEntry = FindMatchingMeshEntry(
                    _currentTaraEntries,
                    AssociatedMeshName
                );
                if (meshEntry != null)
                {
                    long meshLen = new FileInfo(meshPath).Length;
                    return meshEntry.Data.Length == meshLen;
                }
            }

            if (texPath != null)
            {
                TaraEntry? texEntry = FindMatchingTextureEntry(
                    _currentTaraEntries,
                    prop.TextureFilename
                );
                if (texEntry != null)
                {
                    long texLen = new FileInfo(texPath).Length;
                    return texEntry.Data.Length == texLen;
                }
            }

            return false;
        }

        private void OnVersionSelectionChanged()
        {
            if (SelectedProp == null || !HasLcVersion)
            {
                return;
            }

            if (_selectedVariant == PropVariant.LcWinter)
            {
                if (_lcWinterTexturePath != null && File.Exists(_lcWinterTexturePath))
                {
                    _customTextureData = File.ReadAllBytes(_lcWinterTexturePath);
                }
                string? meshP = _lcWinterMeshPath ?? _lcNormalMeshPath;
                if (meshP != null && File.Exists(meshP))
                {
                    _customMeshData = File.ReadAllBytes(meshP);
                }
            }
            else if (_selectedVariant == PropVariant.LcNormal)
            {
                if (_lcNormalTexturePath != null && File.Exists(_lcNormalTexturePath))
                {
                    _customTextureData = File.ReadAllBytes(_lcNormalTexturePath);
                }
                if (_lcNormalMeshPath != null && File.Exists(_lcNormalMeshPath))
                {
                    _customMeshData = File.ReadAllBytes(_lcNormalMeshPath);
                }
            }
            else
            {
                _customTextureData = null;
                _customMeshData = null;
            }

            UpdatePreview();
            OnPropertyChanged(nameof(HasPendingChanges));
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Apply to Game

        private async Task ApplyToGameAsync()
        {
            if (SelectedProp == null || _originalTextureData == null || _currentTaraEntries == null)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (!_mainVM.Settings.IsPremiumUnlocked)
            {
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequired"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequiredDesc"),
                    ControlAppearance.Danger
                );
                return;
            }

            try
            {
                IsWorking = true;
                LoadingMessage = $"Applying modifications for {SelectedProp.Name}...";

                string cachePath = _mainVM.CachePath;
                string cacheFile = Path.Combine(cachePath, SelectedProp.LibraryBase64Url);

                string backupFile = cacheFile + ".bak";
                if (!File.Exists(backupFile))
                {
                    await Task.Run(() => File.Copy(cacheFile, backupFile));
                    Log.Information("Created .tara backup: {Path}", backupFile);
                }

                TaraEntry? matchingTex = FindMatchingTextureEntry(
                    _currentTaraEntries,
                    SelectedProp.TextureFilename
                );
                string actualTexName = matchingTex?.FileName ?? SelectedProp.TextureFilename;
                string actualTexStem = Path.GetFileNameWithoutExtension(actualTexName);

                PropRenderOptions options = BuildRenderOptions();
                byte[] baseData =
                    (_selectedVariant != PropVariant.Default && _customTextureData != null)
                        ? _customTextureData
                        : _originalTextureData;

                byte[] modifiedTextureBytes = await Task.Run(() =>
                    _propComposer.ComposeTexture(baseData, options, targetFilename: actualTexName)
                );

                string? targetMeshName = !string.IsNullOrEmpty(AssociatedMeshName)
                    ? AssociatedMeshName
                    : SelectedProp.MeshFilename;

                byte[]? meshDataToPack = null;
                if (!string.IsNullOrEmpty(targetMeshName))
                {
                    if (_selectedVariant != PropVariant.Default && _customMeshData != null)
                    {
                        meshDataToPack = _customMeshData;
                    }
                    else if (_selectedVariant == PropVariant.Default && _originalMeshData != null)
                    {
                        meshDataToPack = _originalMeshData;
                    }
                }

                Dictionary<string, byte[]> companionTextures = new(
                    StringComparer.OrdinalIgnoreCase
                );
                if (_selectedVariant != PropVariant.Default)
                {
                    string? selectedTexPath =
                        _selectedVariant == PropVariant.LcWinter
                            ? _lcWinterTexturePath
                            : _lcNormalTexturePath;
                    string? selectedMeshPath =
                        _selectedVariant == PropVariant.LcWinter
                            ? (_lcWinterMeshPath ?? _lcNormalMeshPath)
                            : _lcNormalMeshPath;
                    string? selectedLcDir = !string.IsNullOrEmpty(selectedTexPath)
                        ? Path.GetDirectoryName(selectedTexPath)
                        : (
                            !string.IsNullOrEmpty(selectedMeshPath)
                                ? Path.GetDirectoryName(selectedMeshPath)
                                : null
                        );
                    string? lcBaseDir = GetLcDirectory();

                    if (!string.IsNullOrEmpty(selectedLcDir) && Directory.Exists(selectedLcDir))
                    {
                        string stemPrefix = actualTexStem.Replace(
                            "_halloween",
                            "",
                            StringComparison.OrdinalIgnoreCase
                        );
                        foreach (
                            string compFile in Directory.EnumerateFiles(
                                selectedLcDir,
                                $"{actualTexStem}_*.*"
                            )
                        )
                        {
                            string ext = Path.GetExtension(compFile).ToLowerInvariant();
                            if (ext is ".jpg" or ".png")
                            {
                                companionTextures[Path.GetFileName(compFile)] = File.ReadAllBytes(
                                    compFile
                                );
                            }
                        }
                        if (!actualTexStem.Equals(stemPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (
                                string compFile in Directory.EnumerateFiles(
                                    selectedLcDir,
                                    $"{stemPrefix}_*.*"
                                )
                            )
                            {
                                string ext = Path.GetExtension(compFile).ToLowerInvariant();
                                if (ext is ".jpg" or ".png")
                                {
                                    string fName = Path.GetFileName(compFile);
                                    if (!companionTextures.ContainsKey(fName))
                                    {
                                        companionTextures[fName] = File.ReadAllBytes(compFile);
                                    }
                                }
                            }
                        }
                    }

                    HashSet<string> meshTexNames = new(StringComparer.OrdinalIgnoreCase);
                    if (meshDataToPack != null)
                    {
                        foreach (string mt in Mesh3dsHelper.ExtractTextureNames(meshDataToPack))
                        {
                            _ = meshTexNames.Add(mt);
                        }
                    }
                    if (_currentTaraEntries != null)
                    {
                        foreach (TaraEntry entry in _currentTaraEntries)
                        {
                            if (
                                entry.FileName.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase)
                                && entry.Data != null
                            )
                            {
                                foreach (string mt in Mesh3dsHelper.ExtractTextureNames(entry.Data))
                                {
                                    _ = meshTexNames.Add(mt);
                                }
                            }
                        }
                    }

                    if (meshTexNames.Count > 0 && lcBaseDir != null && Directory.Exists(lcBaseDir))
                    {
                        List<string> candidateDirs = [];
                        if (!string.IsNullOrEmpty(selectedLcDir) && Directory.Exists(selectedLcDir))
                        {
                            candidateDirs.Add(selectedLcDir);
                        }
                        foreach (string subDir in Directory.EnumerateDirectories(lcBaseDir))
                        {
                            if (_selectedVariant == PropVariant.LcWinter)
                            {
                                if (
                                    subDir.Contains("winter", StringComparison.OrdinalIgnoreCase)
                                    && !candidateDirs.Contains(
                                        subDir,
                                        StringComparer.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    candidateDirs.Add(subDir);
                                }
                            }
                            else
                            {
                                if (
                                    !subDir.Contains("winter", StringComparison.OrdinalIgnoreCase)
                                    && !candidateDirs.Contains(
                                        subDir,
                                        StringComparer.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    candidateDirs.Add(subDir);
                                }
                            }
                        }
                        foreach (string subDir in Directory.EnumerateDirectories(lcBaseDir))
                        {
                            if (!candidateDirs.Contains(subDir, StringComparer.OrdinalIgnoreCase))
                            {
                                candidateDirs.Add(subDir);
                            }
                        }

                        foreach (string mTex in meshTexNames)
                        {
                            if (companionTextures.ContainsKey(mTex))
                            {
                                continue;
                            }

                            string? foundPath = FindTextureInDirs(mTex, candidateDirs);
                            if (foundPath != null)
                            {
                                byte[] compData = File.ReadAllBytes(foundPath);
                                companionTextures[Path.GetFileName(foundPath)] = compData;
                                if (
                                    !mTex.Equals(
                                        Path.GetFileName(foundPath),
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    companionTextures[mTex] = compData;
                                }
                            }
                        }
                    }
                }
                else if (File.Exists(backupFile))
                {
                    try
                    {
                        List<TaraEntry> bakEntries = _taraService.Unpack(backupFile);
                        string stemPrefix = actualTexStem.Replace(
                            "_halloween",
                            "",
                            StringComparison.OrdinalIgnoreCase
                        );
                        foreach (TaraEntry bEntry in bakEntries)
                        {
                            string bStem = Path.GetFileNameWithoutExtension(bEntry.FileName);
                            if (
                                bStem.StartsWith(
                                    $"{actualTexStem}_",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || bStem.StartsWith(
                                    $"{stemPrefix}_",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                companionTextures[bEntry.FileName] = bEntry.Data;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to load companion textures from backup.");
                    }
                }

                if (_currentTaraEntries == null)
                {
                    return;
                }

                List<TaraEntry> updatedEntries = new(_currentTaraEntries.Count);
                bool texUpdated = false;
                bool meshUpdated = false;

                foreach (TaraEntry e in _currentTaraEntries)
                {
                    string eStem = Path.GetFileNameWithoutExtension(e.FileName);
                    string eCleanStem = eStem.Replace(
                        "_halloween",
                        "",
                        StringComparison.OrdinalIgnoreCase
                    );
                    if (
                        e.FileName.Equals(actualTexName, StringComparison.OrdinalIgnoreCase)
                        || (
                            eStem.Equals(actualTexStem, StringComparison.OrdinalIgnoreCase)
                            && (
                                e.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                || e.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            )
                        )
                    )
                    {
                        updatedEntries.Add(
                            new TaraEntry { FileName = e.FileName, Data = modifiedTextureBytes }
                        );
                        texUpdated = true;
                    }
                    else if (
                        companionTextures.TryGetValue(e.FileName, out byte[]? compData)
                        || companionTextures.TryGetValue(
                            $"{eStem}{Path.GetExtension(e.FileName)}",
                            out compData
                        )
                        || companionTextures.TryGetValue(
                            $"{eCleanStem}{Path.GetExtension(e.FileName)}",
                            out compData
                        )
                    )
                    {
                        updatedEntries.Add(
                            new TaraEntry { FileName = e.FileName, Data = compData }
                        );
                    }
                    else if (
                        meshDataToPack != null
                        && !string.IsNullOrEmpty(targetMeshName)
                        && (
                            e.FileName.Equals(targetMeshName, StringComparison.OrdinalIgnoreCase)
                            || e.FileName.Equals(
                                $"{Path.GetFileNameWithoutExtension(targetMeshName)}.3ds",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || (
                                MeshStemAliases.TryGetValue(
                                    Path.GetFileNameWithoutExtension(targetMeshName),
                                    out string? mAlias
                                )
                                && e.FileName.Equals(
                                    $"{mAlias}.3ds",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        )
                    )
                    {
                        updatedEntries.Add(
                            new TaraEntry { FileName = e.FileName, Data = meshDataToPack }
                        );
                        meshUpdated = true;
                    }
                    else
                    {
                        updatedEntries.Add(e);
                    }
                }

                if (!texUpdated)
                {
                    updatedEntries.Add(
                        new TaraEntry { FileName = actualTexName, Data = modifiedTextureBytes }
                    );
                }

                if (meshDataToPack != null && !string.IsNullOrEmpty(targetMeshName) && !meshUpdated)
                {
                    string finalMeshName = targetMeshName.EndsWith(
                        ".3ds",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? targetMeshName
                        : $"{targetMeshName}.3ds";
                    updatedEntries.Add(
                        new TaraEntry { FileName = finalMeshName, Data = meshDataToPack }
                    );
                }

                foreach ((string? compName, byte[]? compBytes) in companionTextures)
                {
                    if (
                        !updatedEntries.Any(e =>
                            e.FileName.Equals(compName, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        updatedEntries.Add(new TaraEntry { FileName = compName, Data = compBytes });
                    }
                }

                await Task.Run(() => _taraService.Pack(updatedEntries, cacheFile));
                _currentTaraEntries = updatedEntries;

                CurrentInGameVariant = _selectedVariant;

                string appliedSummary = _selectedVariant switch
                {
                    PropVariant.LcWinter => "LC (Legacy Winter) version",
                    PropVariant.LcNormal => "LC (Legacy Normal) version",
                    _ => "Default (Modern) version",
                };

                await _mainVM.NotificationService.ShowAsync(
                    "Success",
                    $"{appliedSummary} for '{SelectedProp.Name}' has been applied to the game cache.",
                    ControlAppearance.Success
                );

                _mainVM.BackupsTabVM.LoadBackups();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply prop modification for {Prop}.", SelectedProp?.Name);
                await _mainVM.NotificationService.ShowAsync(
                    "Error",
                    $"Failed to apply modification: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
            finally
            {
                IsWorking = false;
            }
        }

        #endregion

        #region Mass Batch Apply

        private static bool IsNatureOrTerrainTexture(string stem)
        {
            string s = stem.ToLowerInvariant();
            return s.StartsWith("grass")
                || s.StartsWith("dirt")
                || s.StartsWith("cliff")
                || s.StartsWith("sand")
                || s.StartsWith("terrain")
                || s.StartsWith("rock")
                || s.StartsWith("pave")
                || s.StartsWith("road")
                || s.StartsWith("concrete")
                || s.StartsWith("tree")
                || s.StartsWith("bush")
                || s.StartsWith("yonje")
                || s.StartsWith("zamin")
                || s.StartsWith("khak")
                || s.StartsWith("beton")
                || s.StartsWith("rise")
                || s.StartsWith("slope")
                || s.StartsWith("sm_br")
                || s.StartsWith("bridge")
                || s.StartsWith("wood")
                || s.StartsWith("dd_")
                || s.StartsWith("dg_")
                || s.StartsWith("pg_")
                || s.StartsWith("pc_")
                || s.StartsWith("crater")
                || s.StartsWith("corn")
                || s.StartsWith("land")
                || s.StartsWith("ground")
                || s.StartsWith("tile")
                || s.StartsWith("wtile")
                || s.StartsWith("crack")
                || s.StartsWith("fade")
                || s.StartsWith("shadow")
                || s.StartsWith("foliage")
                || s.StartsWith("plant")
                || s.StartsWith("flower")
                || s.StartsWith("fl_")
                || s.StartsWith("cactus")
                || s.StartsWith("palm")
                || s.StartsWith("water")
                || s.StartsWith("mud")
                || s.StartsWith("earth")
                || s.StartsWith("soil")
                || s.StartsWith("lawn")
                || s.StartsWith("hedge")
                || s.StartsWith("bark")
                || s.StartsWith("trunk")
                || s.StartsWith("leaf")
                || s.StartsWith("leaves")
                || s.StartsWith("snow")
                || s.StartsWith("ice");
        }

        private async Task BatchApplyAsync()
        {
            if (FilteredProps == null || FilteredProps.Count == 0)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (!_mainVM.Settings.IsPremiumUnlocked)
            {
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequired"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequiredDesc"),
                    ControlAppearance.Danger
                );
                return;
            }

            _batchCts = new CancellationTokenSource();
            IsBatching = true;
            IsWorking = true;
            BatchProgress = 0;

            PropRenderOptions options = BuildRenderOptions();
            int total = FilteredProps.Count;
            int success = 0;

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < total; i++)
                    {
                        if (_batchCts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        PropModel prop = FilteredProps[i];
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LoadingMessage = $"Batching ({i + 1}/{total}): {prop.Name}...";
                        });

                        try
                        {
                            string cacheFile = Path.Combine(
                                _mainVM.CachePath,
                                prop.LibraryBase64Url
                            );
                            if (File.Exists(cacheFile))
                            {
                                string backupFile = cacheFile + ".bak";
                                if (!File.Exists(backupFile))
                                {
                                    File.Copy(cacheFile, backupFile);
                                }

                                List<TaraEntry> entries = _taraService.Unpack(cacheFile);
                                TaraEntry? entry = FindMatchingTextureEntry(
                                    entries,
                                    prop.TextureFilename
                                );

                                if (entry != null)
                                {
                                    byte[] modifiedBytes = _propComposer.ComposeTexture(
                                        entry.Data,
                                        options,
                                        targetFilename: entry.FileName
                                    );
                                    entry.Data = modifiedBytes;
                                    _taraService.Pack(entries, cacheFile);
                                    success++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Batch apply failed for {PropName}", prop.Name);
                        }

                        _ = Application.Current.Dispatcher.Invoke(() =>
                            BatchProgress = (double)(i + 1) / total * 100
                        );
                    }
                });

                string msg = _batchCts.Token.IsCancellationRequested
                    ? "Batch cancelled."
                    : "Batch complete.";
                _mainVM.BackupsTabVM.LoadBackups();
                await _mainVM.NotificationService.ShowAsync(
                    "Batch Operation",
                    $"{msg} Successfully applied to {success}/{total} props.",
                    ControlAppearance.Success
                );
            }
            finally
            {
                IsBatching = false;
                IsWorking = false;
                _batchCts?.Dispose();
                _batchCts = null;
            }
        }

        private async Task ApplyAllLcPropsAsync(bool isWinter = false)
        {
            if (_allProps == null || _allProps.Count == 0)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (!_mainVM.Settings.IsPremiumUnlocked)
            {
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequired"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequiredDesc"),
                    ControlAppearance.Danger
                );
                return;
            }

            string? lcDir = GetLcDirectory();
            if (lcDir == null || !Directory.Exists(lcDir))
            {
                await _mainVM.NotificationService.ShowAsync(
                    "LC Assets Not Found",
                    "The legacy assets folder (Textures/Props/LC) was not found.",
                    ControlAppearance.Caution
                );
                return;
            }

            List<IGrouping<string, PropModel>> libraryGroups =
            [
                .. _allProps.GroupBy(p => p.LibraryBase64Url),
            ];

            _batchCts = new CancellationTokenSource();
            IsBatching = true;
            IsWorking = true;
            BatchProgress = 0;

            string modeName = isWinter ? "LC Winter" : "LC Normal";
            int totalLibraries = libraryGroups.Count;
            int librariesModified = 0;
            int propsSwapped = 0;

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < totalLibraries; i++)
                    {
                        if (_batchCts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        IGrouping<string, PropModel> group = libraryGroups[i];
                        string cacheFile = Path.Combine(_mainVM.CachePath, group.Key);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LoadingMessage =
                                $"Applying {modeName} props to library ({i + 1}/{totalLibraries})...";
                        });

                        if (!File.Exists(cacheFile))
                        {
                            _ = Application.Current.Dispatcher.Invoke(() =>
                                BatchProgress = (double)(i + 1) / totalLibraries * 100
                            );
                            continue;
                        }

                        try
                        {
                            List<TaraEntry> entries = _taraService.Unpack(cacheFile);
                            bool modified = false;

                            string? primaryFolder = GetPrimaryLcFolderForLibrary(group.Key, lcDir);

                            List<string> normalDirs = [];
                            List<string> winterDirs = [];

                            if (!string.IsNullOrEmpty(primaryFolder))
                            {
                                string pNorm = Path.Combine(lcDir, primaryFolder);
                                if (Directory.Exists(pNorm))
                                {
                                    normalDirs.Add(pNorm);
                                }

                                string pWint = Path.Combine(lcDir, $"{primaryFolder}_winter");
                                string pWintOld = Path.Combine(
                                    lcDir,
                                    $"{primaryFolder}_winter_old"
                                );
                                string pWintAlt = Path.Combine(
                                    lcDir,
                                    primaryFolder.Replace("_old", "_winter_old")
                                );
                                if (Directory.Exists(pWint))
                                {
                                    winterDirs.Add(pWint);
                                }
                                else if (Directory.Exists(pWintOld))
                                {
                                    winterDirs.Add(pWintOld);
                                }
                                else if (Directory.Exists(pWintAlt))
                                {
                                    winterDirs.Add(pWintAlt);
                                }
                            }

                            List<string> allDirs =
                            [
                                .. Directory
                                    .EnumerateDirectories(lcDir)
                                    .OrderByDescending(d =>
                                    {
                                        string fn = Path.GetFileName(d)
                                            .Replace("_winter_old", "_old")
                                            .Replace("_winter", "");
                                        return FolderPriorityBonuses.TryGetValue(fn, out int b)
                                            ? b
                                            : 0;
                                    }),
                            ];

                            foreach (string subDir in allDirs)
                            {
                                if (
                                    Path.GetFileName(subDir)
                                        .Contains("winter", StringComparison.OrdinalIgnoreCase)
                                )
                                {
                                    if (
                                        !winterDirs.Contains(
                                            subDir,
                                            StringComparer.OrdinalIgnoreCase
                                        )
                                    )
                                    {
                                        winterDirs.Add(subDir);
                                    }
                                }
                                else
                                {
                                    if (
                                        !normalDirs.Contains(
                                            subDir,
                                            StringComparer.OrdinalIgnoreCase
                                        )
                                    )
                                    {
                                        normalDirs.Add(subDir);
                                    }
                                }
                            }

                            foreach (TaraEntry entry in entries)
                            {
                                string ext = Path.GetExtension(entry.FileName).ToLowerInvariant();
                                if (ext is ".jpg" or ".png")
                                {
                                    string stem = Path.GetFileNameWithoutExtension(entry.FileName);
                                    string cleanStem = stem.Replace(
                                        "_halloween",
                                        "",
                                        StringComparison.OrdinalIgnoreCase
                                    );

                                    HashSet<string> texCandidates = new(
                                        StringComparer.OrdinalIgnoreCase
                                    )
                                    {
                                        entry.FileName,
                                        $"{stem}.jpg",
                                        $"{stem}.png",
                                        $"{cleanStem}.jpg",
                                        $"{cleanStem}.png",
                                    };

                                    if (MeshStemAliases.TryGetValue(stem, out string? alias))
                                    {
                                        _ = texCandidates.Add($"{alias}.jpg");
                                        _ = texCandidates.Add($"{alias}.png");
                                    }
                                    if (
                                        MeshStemAliases.TryGetValue(
                                            cleanStem,
                                            out string? cleanAlias
                                        )
                                    )
                                    {
                                        _ = texCandidates.Add($"{cleanAlias}.jpg");
                                        _ = texCandidates.Add($"{cleanAlias}.png");
                                    }

                                    string? foundTexPath = null;
                                    if (isWinter)
                                    {
                                        foreach (string dir in winterDirs)
                                        {
                                            foreach (string cName in texCandidates)
                                            {
                                                string full = Path.Combine(dir, cName);
                                                if (File.Exists(full))
                                                {
                                                    foundTexPath = full;
                                                    break;
                                                }
                                            }
                                            if (foundTexPath != null)
                                            {
                                                break;
                                            }
                                        }

                                        if (foundTexPath == null && !IsNatureOrTerrainTexture(stem))
                                        {
                                            foreach (string dir in normalDirs)
                                            {
                                                foreach (string cName in texCandidates)
                                                {
                                                    string full = Path.Combine(dir, cName);
                                                    if (File.Exists(full))
                                                    {
                                                        foundTexPath = full;
                                                        break;
                                                    }
                                                }
                                                if (foundTexPath != null)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (string dir in normalDirs)
                                        {
                                            foreach (string cName in texCandidates)
                                            {
                                                string full = Path.Combine(dir, cName);
                                                if (File.Exists(full))
                                                {
                                                    foundTexPath = full;
                                                    break;
                                                }
                                            }
                                            if (foundTexPath != null)
                                            {
                                                break;
                                            }
                                        }
                                    }

                                    if (foundTexPath != null)
                                    {
                                        entry.Data = File.ReadAllBytes(foundTexPath);
                                        modified = true;
                                        propsSwapped++;
                                    }
                                }
                                else if (ext == ".3ds")
                                {
                                    string stem = Path.GetFileNameWithoutExtension(entry.FileName);
                                    string cleanStem = stem.Replace(
                                        "_halloween",
                                        "",
                                        StringComparison.OrdinalIgnoreCase
                                    );

                                    HashSet<string> meshCandidates = new(
                                        StringComparer.OrdinalIgnoreCase
                                    )
                                    {
                                        entry.FileName,
                                        $"{stem}.3ds",
                                        $"{cleanStem}.3ds",
                                        $"{stem.Replace("vilhou", "vilhou_")}.3ds",
                                        $"{stem.Replace("vilhou_", "vilhou")}.3ds",
                                    };

                                    if (MeshStemAliases.TryGetValue(stem, out string? mAlias))
                                    {
                                        _ = meshCandidates.Add($"{mAlias}.3ds");
                                    }
                                    if (
                                        MeshStemAliases.TryGetValue(
                                            cleanStem,
                                            out string? mCleanAlias
                                        )
                                    )
                                    {
                                        _ = meshCandidates.Add($"{mCleanAlias}.3ds");
                                    }

                                    string? foundMeshPath = null;
                                    List<string> searchDirs = isWinter
                                        ? [.. winterDirs, .. normalDirs]
                                        : normalDirs;
                                    foreach (string dir in searchDirs)
                                    {
                                        foreach (string mName in meshCandidates)
                                        {
                                            string full = Path.Combine(dir, mName);
                                            if (File.Exists(full))
                                            {
                                                foundMeshPath = full;
                                                break;
                                            }
                                        }
                                        if (foundMeshPath != null)
                                        {
                                            break;
                                        }
                                    }

                                    if (foundMeshPath != null)
                                    {
                                        entry.Data = File.ReadAllBytes(foundMeshPath);
                                        modified = true;
                                    }
                                }
                            }

                            foreach (PropModel prop in group)
                            {
                                if (_batchCts.Token.IsCancellationRequested)
                                {
                                    break;
                                }

                                if (string.IsNullOrEmpty(prop.MeshFilename))
                                {
                                    _ = ResolveMeshFilenameForProp(prop, entries);
                                }

                                if (!string.IsNullOrEmpty(prop.MeshFilename))
                                {
                                    TaraEntry? existingMesh = FindMatchingMeshEntry(
                                        entries,
                                        prop.MeshFilename
                                    );
                                    if (existingMesh == null)
                                    {
                                        List<string> candMeshes = BuildCandidateMeshNames(
                                            prop.MeshFilename,
                                            null,
                                            prop.TextureFilename,
                                            prop.Name
                                        );
                                        string? foundMeshPath = null;
                                        List<string> searchDirs = isWinter
                                            ? [.. winterDirs, .. normalDirs]
                                            : normalDirs;
                                        foreach (string dir in searchDirs)
                                        {
                                            foreach (string mName in candMeshes)
                                            {
                                                string full = Path.Combine(dir, mName);
                                                if (File.Exists(full))
                                                {
                                                    foundMeshPath = full;
                                                    break;
                                                }
                                            }
                                            if (foundMeshPath != null)
                                            {
                                                break;
                                            }
                                        }

                                        if (foundMeshPath != null)
                                        {
                                            string mName = prop.MeshFilename.EndsWith(
                                                ".3ds",
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                                ? prop.MeshFilename
                                                : $"{prop.MeshFilename}.3ds";
                                            entries.Add(
                                                new TaraEntry
                                                {
                                                    FileName = mName,
                                                    Data = File.ReadAllBytes(foundMeshPath),
                                                }
                                            );
                                            modified = true;
                                        }
                                    }
                                }
                            }

                            {
                                List<string> allSearchDirs = isWinter
                                    ? [.. winterDirs, .. normalDirs]
                                    : normalDirs;
                                HashSet<string> neededTextures = new(
                                    StringComparer.OrdinalIgnoreCase
                                );

                                foreach (TaraEntry entry in entries)
                                {
                                    if (
                                        entry.FileName.EndsWith(
                                            ".3ds",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                        && entry.Data != null
                                    )
                                    {
                                        HashSet<string> meshTexs =
                                            Mesh3dsHelper.ExtractTextureNames(entry.Data);
                                        foreach (string mt in meshTexs)
                                        {
                                            _ = neededTextures.Add(mt);
                                        }
                                    }
                                }

                                HashSet<string> archiveTexStems = new(
                                    StringComparer.OrdinalIgnoreCase
                                );
                                foreach (TaraEntry entry in entries)
                                {
                                    string ext = Path.GetExtension(entry.FileName)
                                        .ToLowerInvariant();
                                    if (ext is ".jpg" or ".png")
                                    {
                                        _ = archiveTexStems.Add(
                                            Path.GetFileNameWithoutExtension(entry.FileName)
                                        );
                                    }
                                }
                                foreach (PropModel prop in group)
                                {
                                    if (!string.IsNullOrEmpty(prop.TextureFilename))
                                    {
                                        _ = archiveTexStems.Add(
                                            Path.GetFileNameWithoutExtension(prop.TextureFilename)
                                        );
                                    }
                                }

                                foreach (string dir in allSearchDirs)
                                {
                                    if (!Directory.Exists(dir))
                                    {
                                        continue;
                                    }

                                    foreach (string tStem in archiveTexStems)
                                    {
                                        string cleanStem = tStem.Replace(
                                            "_halloween",
                                            "",
                                            StringComparison.OrdinalIgnoreCase
                                        );
                                        foreach (
                                            string compFile in Directory.EnumerateFiles(
                                                dir,
                                                $"{tStem}_*.*"
                                            )
                                        )
                                        {
                                            string ext = Path.GetExtension(compFile)
                                                .ToLowerInvariant();
                                            if (ext is ".jpg" or ".png")
                                            {
                                                _ = neededTextures.Add(Path.GetFileName(compFile));
                                            }
                                        }
                                        if (
                                            !cleanStem.Equals(
                                                tStem,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        )
                                        {
                                            foreach (
                                                string compFile in Directory.EnumerateFiles(
                                                    dir,
                                                    $"{cleanStem}_*.*"
                                                )
                                            )
                                            {
                                                string ext = Path.GetExtension(compFile)
                                                    .ToLowerInvariant();
                                                if (ext is ".jpg" or ".png")
                                                {
                                                    _ = neededTextures.Add(
                                                        Path.GetFileName(compFile)
                                                    );
                                                }
                                            }
                                        }
                                    }
                                }

                                foreach (string nTex in neededTextures)
                                {
                                    string? foundTex = FindTextureInDirs(nTex, allSearchDirs);
                                    if (foundTex != null)
                                    {
                                        byte[] tBytes = File.ReadAllBytes(foundTex);
                                        string finalTexName = Path.GetFileName(foundTex);
                                        TaraEntry? existing = entries.FirstOrDefault(e =>
                                            e.FileName.Equals(
                                                nTex,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                            || e.FileName.Equals(
                                                finalTexName,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        );

                                        if (existing != null)
                                        {
                                            if (
                                                existing.Data == null
                                                || existing.Data.Length != tBytes.Length
                                                || !existing.Data.SequenceEqual(tBytes)
                                            )
                                            {
                                                existing.Data = tBytes;
                                                modified = true;
                                            }
                                        }
                                        else
                                        {
                                            entries.Add(
                                                new TaraEntry
                                                {
                                                    FileName = finalTexName,
                                                    Data = tBytes,
                                                }
                                            );
                                            if (
                                                !finalTexName.Equals(
                                                    nTex,
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                                && !entries.Any(e =>
                                                    e.FileName.Equals(
                                                        nTex,
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                )
                                            )
                                            {
                                                entries.Add(
                                                    new TaraEntry { FileName = nTex, Data = tBytes }
                                                );
                                            }
                                            modified = true;
                                            propsSwapped++;
                                        }
                                    }
                                }
                            }

                            if (modified)
                            {
                                string backupFile = cacheFile + ".bak";
                                if (!File.Exists(backupFile))
                                {
                                    File.Copy(cacheFile, backupFile);
                                    Log.Information(
                                        "Created .tara backup before batch LC: {Path}",
                                        backupFile
                                    );
                                }

                                _taraService.Pack(entries, cacheFile);
                                librariesModified++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to apply LC props to library {Lib}", group.Key);
                        }

                        _ = Application.Current.Dispatcher.Invoke(() =>
                            BatchProgress = (double)(i + 1) / totalLibraries * 100
                        );
                    }
                });

                string msg = _batchCts.Token.IsCancellationRequested
                    ? "Operation cancelled."
                    : "Operation complete.";
                _mainVM.BackupsTabVM.LoadBackups();

                if (SelectedProp != null)
                {
                    _currentTaraBase64Url = null;
                    _currentTaraEntries = null;
                    await LoadSelectedPropAsync();
                }

                await _mainVM.NotificationService.ShowAsync(
                    $"{modeName} Props Applied",
                    $"Successfully applied {modeName} version to {propsSwapped} props across {librariesModified} libraries.",
                    ControlAppearance.Success
                );
            }
            finally
            {
                IsBatching = false;
                IsWorking = false;
                _batchCts?.Dispose();
                _batchCts = null;
            }
        }

        #endregion

        #region Restore Original

        private async Task RestoreOriginalAsync()
        {
            if (SelectedProp == null)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            try
            {
                IsWorking = true;
                LoadingMessage = $"Restoring original library for {SelectedProp.Name}...";

                string cachePath = _mainVM.CachePath;
                string cacheFile = Path.Combine(cachePath, SelectedProp.LibraryBase64Url);
                string backupFile = cacheFile + ".bak";

                if (!File.Exists(backupFile))
                {
                    await _mainVM.NotificationService.ShowAsync(
                        "No Backup",
                        "No backup (.bak) file found for this library. It may not have been modified yet.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                await Task.Run(() => File.Copy(backupFile, cacheFile, overwrite: true));

                _currentTaraBase64Url = null;
                _currentTaraEntries = null;
                _customTextureData = null;
                _customMeshData = null;

                await LoadSelectedPropAsync();

                await _mainVM.NotificationService.ShowAsync(
                    "Restored",
                    $"Original library for '{SelectedProp.Name}' has been restored.",
                    ControlAppearance.Success
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore original for {Prop}.", SelectedProp?.Name);
            }
            finally
            {
                IsWorking = false;
            }
        }

        #endregion
    }
}
