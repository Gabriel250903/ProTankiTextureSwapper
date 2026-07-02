using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly INotificationService _notificationService;
        private string _passwordInput = string.Empty;
        private bool _isAuthenticated;
        private string _searchText = string.Empty;
        private List<SkinModel> _allPaints = [];
        private readonly JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string PasswordInput
        {
            get => _passwordInput;
            set => SetProperty(ref _passwordInput, value);
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                if (SetProperty(ref _isAuthenticated, value))
                {
                    if (value)
                    {
                        LoadPaints();
                    }
                }
            }
        }

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

        public ObservableCollection<SkinModel> FilteredPaints { get; } = [];
        public ObservableCollection<string> ExistingItemNames { get; } = [];
        public ObservableCollection<string> FilterCategories { get; } = [];

        public ICommand LoginCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddCategoryCommand { get; }
        public ICommand RemoveCategoryCommand { get; }
        public ICommand RenameCategoryCommand { get; }

        public event Action? CloseRequested;

        public AdminViewModel(MainViewModel mainViewModel, INotificationService notificationService)
        {
            _mainViewModel = mainViewModel;
            _notificationService = notificationService;

            LoginCommand = new RelayCommand(ExecuteLogin);
            SaveChangesCommand = new AsyncRelayCommand(ExecuteSaveChanges);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
            AddCategoryCommand = new RelayCommand(ExecuteAddCategory);
            RemoveCategoryCommand = new RelayCommand(ExecuteRemoveCategory);
            RenameCategoryCommand = new RelayCommand(ExecuteRenameCategory);
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
                _ = _notificationService.ShowAsync("Access Denied", $"Cannot rename system category '{oldName}'.", ControlAppearance.Danger);
                return;
            }

            if (ExistingItemNames.Contains(newName))
            {
                _ = _notificationService.ShowAsync("Info", "A folder category with that name already exists.", ControlAppearance.Caution);
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
                Log.Information($"Renamed category '{oldName}' to '{newName}'. Updated {count} {countPaints}.");
                _ = _notificationService.ShowAsync("Success", $"Renamed '{oldName}' to '{newName}'. {count} paints updated.", ControlAppearance.Success);
            }
        }

        private void RebuildFilterCategories()
        {
            string currentSelected = SelectedFilterCategory;
            FilterCategories.Clear();
            FilterCategories.Add("All");
            foreach (string name in ExistingItemNames)
            {
                FilterCategories.Add(name);
            }
            SelectedFilterCategory = FilterCategories.Contains(currentSelected) ? currentSelected : "All";
        }

        private void ExecuteAddCategory(object? parameter)
        {
            string name = NewCategoryName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!ExistingItemNames.Contains(name))
            {
                ExistingItemNames.Add(name);
                RebuildFilterCategories();
                Log.Information($"Added new paint category option: {name}");
                _ = _notificationService.ShowAsync("Success", $"Category folder '{name}' added to choices.", ControlAppearance.Success);
                NewCategoryName = string.Empty;
            }
            else
            {
                _ = _notificationService.ShowAsync("Info", "This folder category choice already exists.", ControlAppearance.Caution);
            }
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
                _ = _notificationService.ShowAsync("Access Denied", $"Cannot delete system category '{cat}'.", ControlAppearance.Danger);
                return;
            }

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
                Log.Information($"Removed paint category option: {cat}. Reassigned {count} {countPaints} to 'Custom Paints'.");
                _ = _notificationService.ShowAsync("Success", $"Deleted '{cat}'. {count} paints reassigned to 'Custom Paints'.", ControlAppearance.Success);
            }
        }

        private void ExecuteLogin(object? parameter)
        {
            string trimmedInput = PasswordInput?.Trim() ?? string.Empty;
            Log.Information("ExecuteLogin called. Input length: {InputLength}, Trimmed length: {TrimmedLength}", PasswordInput?.Length ?? 0, trimmedInput.Length);

            string expectedHash = _mainViewModel.Settings.AdminPasswordHash;
            string salt = _mainViewModel.Settings.AdminPasswordSalt;

            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)
                || expectedHash.Equals("240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9", StringComparison.OrdinalIgnoreCase)
                || expectedHash.Equals("240751AF9EDC645A95A85A4815F54C546FF871D7374BE403E5D4665476718BD8", StringComparison.OrdinalIgnoreCase)
                || expectedHash.Equals("01B307ACBA4F54F55AAFC33BB06BBBF6CA803E9A37C083E4672B1BBA4CAE54E4", StringComparison.OrdinalIgnoreCase)
                || expectedHash.Equals("A3CB853E34B95F19DB1D3F9B2D354B679A9F24B22F01F09BBCB04EB921C57EA1", StringComparison.OrdinalIgnoreCase))
            {
                salt = "DEFAULT_SALT_123";
                expectedHash = "19FFFAF056A656FB4A13BAB7F8829D8C6B35C7C197C9629C42E07A3F7981CB68";

                _mainViewModel.Settings.AdminPasswordSalt = salt;
                _mainViewModel.Settings.AdminPasswordHash = expectedHash;
                _mainViewModel._settingsService.Save(_mainViewModel.Settings);
                Log.Information("Migrated admin password config to salted hash schema.");
            }

            string hashedInput = HashPassword(trimmedInput.ToLower(), salt);
            Log.Information("ExecuteLogin: Performing admin authentication verification.");
            if (hashedInput.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                IsAuthenticated = true;
                PasswordInput = string.Empty;
                Log.Information("Admin successfully authenticated.");
            }
            else
            {
                IsAuthenticated = false;
                _ = _notificationService.ShowAsync("Authentication Failed", "Incorrect admin password.", ControlAppearance.Danger);
                Log.Warning("Failed admin authentication attempt.");
            }
        }

        private string HashPassword(string password, string saltValue)
        {
            byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password + saltValue));
            return Convert.ToHexString(bytes);
        }

        private void LoadPaints()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.PaintsSkinsJson);
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    _allPaints = JsonSerializer.Deserialize<List<SkinModel>>(json) ?? [];
                }
                else
                {
                    _allPaints = [];
                }

                List<string> names = [.. _allPaints.Select(p => p.ItemName).Distinct().Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n)];
                ExistingItemNames.Clear();
                foreach (string name in names)
                {
                    ExistingItemNames.Add(name);
                }

                string[] defaults = ["Custom Paints", "Special", "Moderator", "Admin", "Premium"];
                foreach (string def in defaults)
                {
                    if (!ExistingItemNames.Contains(def))
                    {
                        ExistingItemNames.Add(def);
                    }
                }

                RebuildFilterCategories();
                FilterPaints();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load paints in Admin View.");
                _ = _notificationService.ShowAsync("Error", "Failed to load custom paints.", ControlAppearance.Danger);
            }
        }

        private void FilterPaints()
        {
            FilteredPaints.Clear();
            IEnumerable<SkinModel> filtered = _allPaints;

            if (!string.IsNullOrEmpty(SelectedFilterCategory) && SelectedFilterCategory != "All")
            {
                filtered = filtered.Where(p => p.ItemName == SelectedFilterCategory);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (p.ItemName != null && p.ItemName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (SkinModel paint in filtered)
            {
                FilteredPaints.Add(paint);
            }
        }

        private async Task ExecuteSaveChanges(object? parameter)
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.PaintsSkinsJson);
                Log.Information($"Saving admin category reassignments back to disk: {jsonPath}");

                string json = JsonSerializer.Serialize(_allPaints, options);

                await File.WriteAllTextAsync(jsonPath, json);
                Log.Information("Successfully wrote updated paints list to output disk.");

                try
                {
                    string sourceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                    string sourceJsonPath = Path.Combine(sourceDir, Constants.PaintsSkinsJson);
                    if (File.Exists(sourceJsonPath))
                    {
                        Log.Information($"Project source directory detected. Saving update to project source: {sourceJsonPath}");
                        await File.WriteAllTextAsync(sourceJsonPath, json);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not write back to source path (normal for release runs).");
                }

                await _notificationService.ShowAsync("Success", "Category assignments saved successfully.", ControlAppearance.Success);

                if (_mainViewModel != null)
                {
                    await _mainViewModel.ReloadSkinsDataAsync();
                }

                CloseRequested?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save paint category changes to disk.");
                await _notificationService.ShowAsync("Error", $"Failed to save changes: {ex.Message}", ControlAppearance.Danger);
            }
        }
    }
}
