using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;

namespace TextureSwapper.ViewModels.Base
{
    public abstract class TextureEditorViewModelBase : ViewModelBase
    {
        protected readonly ITextureComposer Composer;
        protected readonly ISwapService SwapService;
        protected readonly INotificationService NotificationService;
        protected readonly MainViewModel MainVM;

        private string? _sourceImagePath;
        private double _scale = 1.0;
        private double _offsetX;
        private double _offsetY;
        private double _rotationAngle;
        private bool _tileMode;
        private SkinModel? _selectedPaint;
        private string _paintSearchQuery = string.Empty;
        private bool _isApplying;

        public ObservableCollection<SkinModel> AvailablePaints { get; } = [];
        public ObservableCollection<SkinModel> FilteredPaints { get; } = [];

        public bool IsPremiumUnlocked => MainVM.Settings.IsPremiumUnlocked;
        public bool IsPremiumLocked => !IsPremiumUnlocked;

        public ICommand BuyPremiumCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public string? SourceImagePath
        {
            get => _sourceImagePath;
            set
            {
                if (SetProperty(ref _sourceImagePath, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        AutoScaleForImage(value);
                    }
                    else
                    {
                        UpdatePreview();
                    }
                }
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    UpdatePreview();
                }
            }
        }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (SetProperty(ref _offsetX, value))
                {
                    UpdatePreview();
                }
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (SetProperty(ref _offsetY, value))
                {
                    UpdatePreview();
                }
            }
        }

        public double RotationAngle
        {
            get => _rotationAngle;
            set
            {
                if (SetProperty(ref _rotationAngle, value))
                {
                    UpdatePreview();
                }
            }
        }

        public bool TileMode
        {
            get => _tileMode;
            set
            {
                if (SetProperty(ref _tileMode, value))
                {
                    UpdatePreview();
                }
            }
        }

        public SkinModel? SelectedPaint
        {
            get => _selectedPaint;
            set
            {
                if (SetProperty(ref _selectedPaint, value) && value != null)
                {
                    string fullPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        value.PreviewImage
                    );
                    SourceImagePath = fullPath;
                }
            }
        }

        public string PaintSearchQuery
        {
            get => _paintSearchQuery;
            set
            {
                if (SetProperty(ref _paintSearchQuery, value))
                {
                    FilterPaints();
                }
            }
        }

        public bool IsApplying
        {
            get => _isApplying;
            set => SetProperty(ref _isApplying, value);
        }

        public ICommand BrowseImageCommand { get; }
        public ICommand ResetTransformCommand { get; }

        protected TextureEditorViewModelBase(
            ITextureComposer composer,
            ISwapService swapService,
            INotificationService notificationService,
            MainViewModel mainVM
        )
        {
            Composer = composer;
            SwapService = swapService;
            NotificationService = notificationService;
            MainVM = mainVM;

            BrowseImageCommand = new RelayCommand(ExecuteBrowseImage);
            ResetTransformCommand = new RelayCommand(ExecuteResetTransform);
            BuyPremiumCommand = new RelayCommand(_ =>
            {
                try
                {
                    _ = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "https://flytoxp.gumroad.com/l/sebwv",
                            UseShellExecute = true,
                        }
                    );
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to open premium link.");
                }
            });
            OpenSettingsCommand = new RelayCommand(_ =>
            {
                MainVM.OpenSettingsCommand.Execute(null);
            });

            MainVM.PremiumService.PremiumStatusChanged += (_, _) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsPremiumUnlocked));
                    OnPropertyChanged(nameof(IsPremiumLocked));
                });
            };

            LoadPaintsList();
        }

        public abstract void UpdatePreview();

        protected virtual void AutoScaleForImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            try
            {
                BitmapImage bmp = new();
                bmp.BeginInit();
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                double imgW = bmp.PixelWidth;
                double imgH = bmp.PixelHeight;

                if (imgW > 0 && imgH > 0)
                {
                    double targetDiameter = 438.0;
                    double minDim = Math.Min(imgW, imgH);
                    double calculatedScale = Math.Round(targetDiameter / minDim, 2);
                    _scale = Math.Clamp(calculatedScale, 0.1, 25.0);
                    _offsetX = 0.0;
                    _offsetY = 0.0;

                    OnPropertyChanged(nameof(Scale));
                    OnPropertyChanged(nameof(OffsetX));
                    OnPropertyChanged(nameof(OffsetY));

                    UpdatePreview();
                }
            }
            catch
            {
                UpdatePreview();
            }
        }

        protected virtual void LoadPaintsList()
        {
            if (MainVM.AllSkins == null || MainVM.AllSkins.Count == 0)
            {
                return;
            }

            AvailablePaints.Clear();
            foreach (
                SkinModel paint in MainVM.AllSkins.Where(s =>
                    s.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                AvailablePaints.Add(paint);
            }
            FilterPaints();
        }

        protected virtual void FilterPaints()
        {
            FilteredPaints.Clear();
            IEnumerable<SkinModel> query = AvailablePaints;
            if (!string.IsNullOrWhiteSpace(PaintSearchQuery))
            {
                query = query.Where(p =>
                    p.Name.Contains(PaintSearchQuery, StringComparison.OrdinalIgnoreCase)
                    || (
                        p.ItemName != null
                        && p.ItemName.Contains(PaintSearchQuery, StringComparison.OrdinalIgnoreCase)
                    )
                );
            }
            foreach (SkinModel paint in query)
            {
                FilteredPaints.Add(paint);
            }
        }

        protected virtual void ExecuteBrowseImage(object? obj)
        {
            OpenFileDialog dlg = new()
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*",
                Title = "Select Custom Texture",
            };

            if (dlg.ShowDialog() == true)
            {
                Composer.ClearCache();
                SelectedPaint = null;
                SourceImagePath = dlg.FileName;
            }
        }

        protected virtual void ExecuteResetTransform(object? obj)
        {
            _scale = 1.0;
            _offsetX = 0.0;
            _offsetY = 0.0;
            _rotationAngle = 0.0;
            _tileMode = false;
            _sourceImagePath = null;
            _selectedPaint = null;
            _paintSearchQuery = string.Empty;

            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(RotationAngle));
            OnPropertyChanged(nameof(TileMode));
            OnPropertyChanged(nameof(SourceImagePath));
            OnPropertyChanged(nameof(SelectedPaint));
            OnPropertyChanged(nameof(PaintSearchQuery));

            Composer.ClearCache();
            FilterPaints();
            UpdatePreview();
        }
    }
}
