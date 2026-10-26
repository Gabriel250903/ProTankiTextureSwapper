using System.IO;
using System.Text.Json;
using System.Windows;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class SkinSyncService(
        IUpdateService updateService,
        ISettingsService settingsService
    ) : ISkinSyncService
    {
        private readonly IUpdateService _updateService = updateService;
        private readonly ISettingsService _settingsService = settingsService;
        public event Action<string>? ProgressChanged;
        public event Action<SkinModel>? SkinPreviewUpdated;
        private readonly JsonSerializerOptions options = new() { WriteIndented = true };

        public async Task<(
            List<SkinModel> Skins,
            List<InGamePaintModel>? RemoteInGamePaints
        )> SyncAndLoadSkinsAsync(CancellationToken cancellationToken = default)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localHullsPath = Path.Combine(baseDir, Constants.HullsSkinsJson);
            string localTurretsPath = Path.Combine(baseDir, Constants.TurretsSkinsJson);
            string localSuppliesPath = Path.Combine(baseDir, Constants.SuppliesSkinsJson);
            string localSpawnersPath = Path.Combine(baseDir, Constants.SpawnersSkinsJson);
            string localPaintsPath = Path.Combine(baseDir, Constants.PaintsSkinsJson);

            (string Name, string LocalPath, string RemoteFileName)[] categories =
            [
                (
                    Name: "Hulls",
                    LocalPath: localHullsPath,
                    RemoteFileName: Constants.HullsSkinsJson
                ),
                (
                    Name: "Turrets",
                    LocalPath: localTurretsPath,
                    RemoteFileName: Constants.TurretsSkinsJson
                ),
                (
                    Name: "Supplies",
                    LocalPath: localSuppliesPath,
                    RemoteFileName: Constants.SuppliesSkinsJson
                ),
                (
                    Name: "Spawners",
                    LocalPath: localSpawnersPath,
                    RemoteFileName: Constants.SpawnersSkinsJson
                ),
                (
                    Name: "Paints",
                    LocalPath: localPaintsPath,
                    RemoteFileName: Constants.PaintsSkinsJson
                ),
            ];

            List<SkinModel> combinedSkins = [];
            List<InGamePaintModel>? remoteInGamePaints = null;

            foreach ((string Name, string LocalPath, string RemoteFileName) in categories)
            {
                string localJson = File.Exists(LocalPath)
                    ? await File.ReadAllTextAsync(LocalPath, cancellationToken)
                    : string.Empty;
                List<SkinModel> catSkins = [];
                if (!string.IsNullOrEmpty(localJson))
                {
                    try
                    {
                        catSkins = JsonSerializer.Deserialize<List<SkinModel>>(localJson) ?? [];
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, $"Failed to deserialize local {Name} skins.");
                    }
                }

                if (_updateService.IsOffline)
                {
                    combinedSkins.AddRange(catSkins);
                    continue;
                }

                ProgressChanged?.Invoke($"Syncing {Name.ToLower()} database...");
                (List<SkinModel>? remoteSkins, string? remoteJson) =
                    await _updateService.FetchRemoteSkinsFileAsync(RemoteFileName);

                if (remoteSkins != null && remoteJson != localJson)
                {
                    bool skipTargets = _settingsService.Load().SkipRemoteTargetSync;
                    Log.Information(
                        $"Remote {RemoteFileName} is different from local. Merging changes... (PreserveLocalTargets: {skipTargets})"
                    );
                    bool merged = false;
                    foreach (SkinModel remoteSkin in remoteSkins)
                    {
                        SkinModel? localSkin = catSkins.FirstOrDefault(s =>
                            s.Name.Equals(remoteSkin.Name, StringComparison.OrdinalIgnoreCase)
                        );
                        if (localSkin != null)
                        {
                            bool targetsChanged =
                                localSkin.DetailsTarget != remoteSkin.DetailsTarget
                                || localSkin.LightmapTarget != remoteSkin.LightmapTarget
                                || localSkin.AlphaTarget != remoteSkin.AlphaTarget
                                || localSkin.ModelTarget != remoteSkin.ModelTarget;

                            bool isLocalEncrypted = localSkin.SourceFolder.StartsWith("Textures/Encrypted", StringComparison.OrdinalIgnoreCase);

                            bool metaChanged = !isLocalEncrypted && (
                                localSkin.SourceFolder != remoteSkin.SourceFolder
                                || localSkin.PreviewImage != remoteSkin.PreviewImage
                            );

                            if (targetsChanged || metaChanged)
                            {
                                if (!skipTargets)
                                {
                                    localSkin.DetailsTarget = remoteSkin.DetailsTarget;
                                    localSkin.LightmapTarget = remoteSkin.LightmapTarget;
                                    localSkin.AlphaTarget = remoteSkin.AlphaTarget;
                                    localSkin.ModelTarget = remoteSkin.ModelTarget;
                                }
                                else if (targetsChanged)
                                {
                                    Log.Information(
                                        "Preserve Local Target URLs enabled: preserved local targets for {SkinName}.",
                                        localSkin.Name
                                    );
                                }

                                if (!isLocalEncrypted)
                                {
                                    localSkin.SourceFolder = remoteSkin.SourceFolder;
                                    localSkin.PreviewImage = remoteSkin.PreviewImage;
                                }
                                merged = true;
                            }
                        }
                        else
                        {
                            catSkins.Add(remoteSkin);
                            merged = true;
                        }
                    }

                    HashSet<string> remoteNames = new(
                        remoteSkins.Select(s => s.Name),
                        StringComparer.OrdinalIgnoreCase
                    );
                    int removed = catSkins.RemoveAll(s =>
                        !remoteNames.Contains(s.Name)
                        && s.ItemName != "Uploaded"
                        && !s.SourceFolder.StartsWith("Textures/Encrypted", StringComparison.OrdinalIgnoreCase)
                        && !s.IsPremium
                        && IsSkinMissingAssets(s)
                    );
                    if (removed > 0)
                    {
                        merged = true;
                    }

                    if (merged)
                    {
                        string mergedJson = JsonSerializer.Serialize(catSkins, options);
                        await File.WriteAllTextAsync(LocalPath, mergedJson, cancellationToken);
                    }
                }

                List<SkinModel> missing =
                [
                    .. catSkins.Where(s => s.ItemName != "Uploaded" && IsSkinMissingAssets(s)),
                ];
                if (missing.Count > 25)
                {
                    Log.Warning(
                        "{Count} {Category} skins are missing. Skipping individual file sync to prevent GitHub rate limits.",
                        missing.Count,
                        Name
                    );
                }
                else if (missing.Count > 0 && !_updateService.IsOffline)
                {
                    int completed = 0;
                    object progressLock = new();
                    using SemaphoreSlim semaphore = new(5);
                    List<Task> downloadTasks = [];

                    foreach (SkinModel skin in missing)
                    {
                        downloadTasks.Add(
                            Task.Run(async () =>
                            {
                                await semaphore.WaitAsync();
                                try
                                {
                                    lock (progressLock)
                                    {
                                        ProgressChanged?.Invoke(
                                            $"Syncing {Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name}..."
                                        );
                                    }

                                    await _updateService.EnsureAssetsExistAsync(
                                        skin,
                                        p =>
                                        {
                                            lock (progressLock)
                                            {
                                                ProgressChanged?.Invoke(
                                                    $"Syncing {Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name} - {p}"
                                                );
                                            }
                                        }
                                    );

                                    Application.Current?.Dispatcher.Invoke(() =>
                                    {
                                        skin.NotifyPreviewChanged();
                                        SkinPreviewUpdated?.Invoke(skin);
                                    });
                                }
                                finally
                                {
                                    int currentCompleted = Interlocked.Increment(ref completed);
                                    lock (progressLock)
                                    {
                                        ProgressChanged?.Invoke(
                                            $"Syncing {Name.ToLower()} ({currentCompleted}/{missing.Count})..."
                                        );
                                    }
                                    _ = semaphore.Release();
                                }
                            }, cancellationToken)
                        );
                    }

                    await Task.WhenAll(downloadTasks);
                }

                foreach (SkinModel skin in catSkins)
                {
                    if (
                        skin.Name.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                        || skin.SourceFolder.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                        || skin.Name.Contains(" DC", StringComparison.OrdinalIgnoreCase)
                        || skin.SourceFolder.Contains("/DC/", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        skin.IsPremium = true;
                    }
                }

                combinedSkins.AddRange(catSkins);
            }

            try
            {
                string localInGamePath = Path.Combine(baseDir, Constants.InGamePaintsJson);
                string localInGameJson = File.Exists(localInGamePath)
                    ? await File.ReadAllTextAsync(localInGamePath, cancellationToken)
                    : string.Empty;
                ProgressChanged?.Invoke("Syncing in-game paints database...");
                (List<InGamePaintModel>? fetchedPaints, string? remoteInGameJson) =
                    await _updateService.FetchRemoteInGamePaintsFileAsync(
                        Constants.InGamePaintsJson
                    );
                if (fetchedPaints != null && remoteInGameJson != localInGameJson)
                {
                    Log.Information(
                        "Remote ingame_paints.json is different from local. Updating..."
                    );
                    await File.WriteAllTextAsync(localInGamePath, remoteInGameJson!, cancellationToken);
                    remoteInGamePaints = fetchedPaints;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to sync remote ingame_paints.json");
            }

            return (combinedSkins, remoteInGamePaints);
        }

        public async Task<List<ShotEffectModel>> SyncAndLoadShotEffectsAsync(
            CancellationToken cancellationToken = default
        )
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, Constants.ShotEffectsJson);

            string localJson = File.Exists(localPath)
                ? await File.ReadAllTextAsync(localPath, cancellationToken)
                : string.Empty;
            List<ShotEffectModel> shotEffects = [];
            if (!string.IsNullOrEmpty(localJson))
            {
                try
                {
                    shotEffects =
                        JsonSerializer.Deserialize<List<ShotEffectModel>>(localJson) ?? [];
                }
                catch (JsonException ex)
                {
                    Log.Error(ex, "Failed to deserialize local shot effects.");
                }
            }

            if (_updateService.IsOffline)
            {
                return shotEffects;
            }

            try
            {
                ProgressChanged?.Invoke("Syncing shot effects database...");
                (List<ShotEffectModel>? remoteEffects, string? remoteJson) =
                    await _updateService.FetchRemoteShotEffectsFileAsync(Constants.ShotEffectsJson);

                if (remoteEffects != null && remoteJson != localJson)
                {
                    bool skipTargets = _settingsService.Load().SkipRemoteTargetSync;
                    Log.Information(
                        $"Remote shot_effects.json is different from local. Merging changes... (PreserveLocalTargets: {skipTargets})"
                    );
                    bool merged = false;
                    foreach (ShotEffectModel remoteEffect in remoteEffects)
                    {
                        ShotEffectModel? localEffect = shotEffects.FirstOrDefault(s =>
                            s.Turret.Equals(remoteEffect.Turret, StringComparison.OrdinalIgnoreCase)
                            && s.Name.Equals(remoteEffect.Name, StringComparison.OrdinalIgnoreCase)
                        );
                        if (localEffect != null)
                        {
                            bool targetsChanged =
                                string.Join(",", localEffect.Targets)
                                != string.Join(",", remoteEffect.Targets);
                            bool metaChanged =
                                !localEffect.SourceFolder.Equals(
                                    remoteEffect.SourceFolder,
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || !localEffect.PreviewImage.Equals(
                                    remoteEffect.PreviewImage,
                                    StringComparison.OrdinalIgnoreCase
                                );

                            if (targetsChanged || metaChanged)
                            {
                                localEffect.SourceFolder = remoteEffect.SourceFolder;
                                localEffect.PreviewImage = remoteEffect.PreviewImage;
                                if (!skipTargets)
                                {
                                    localEffect.Targets = remoteEffect.Targets;
                                }
                                else if (targetsChanged)
                                {
                                    Log.Information(
                                        "Preserve Local Target URLs enabled: preserved local shot effect targets for {Turret} - {Name}.",
                                        localEffect.Turret,
                                        localEffect.Name
                                    );
                                }
                                merged = true;
                            }
                        }
                        else
                        {
                            shotEffects.Add(remoteEffect);
                            merged = true;
                        }
                    }

                    HashSet<string> remoteKeys = new(
                        remoteEffects.Select(s => $"{s.Turret}:{s.Name}"),
                        StringComparer.OrdinalIgnoreCase
                    );
                    int removed = shotEffects.RemoveAll(s =>
                        !remoteKeys.Contains($"{s.Turret}:{s.Name}")
                    );
                    if (removed > 0)
                    {
                        merged = true;
                    }

                    if (merged)
                    {
                        string mergedJson = JsonSerializer.Serialize(shotEffects, options);
                        await File.WriteAllTextAsync(localPath, mergedJson, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to sync remote shot_effects.json");
            }

            return shotEffects;
        }

        public List<BackupModel> LoadBackups()
        {
            string backupsRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.BackupsDir
            );
            if (!Directory.Exists(backupsRoot))
            {
                return [];
            }

            List<BackupModel> backups = [];

            string[] zipFiles = Directory.GetFiles(backupsRoot, "*.zip");
            foreach (string file in zipFiles)
            {
                FileInfo fi = new(file);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                backups.Add(
                    new BackupModel
                    {
                        FolderName = fi.Name,
                        DisplayName = nameWithoutExt.Replace("_", " "),
                        CreationDate = fi.CreationTime,
                        FullPath = fi.FullName,
                    }
                );
            }

            return [.. backups.OrderByDescending(b => b.CreationDate)];
        }

        public List<InGamePaintModel> LoadInGamePaints()
        {
            string jsonPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.InGamePaintsJson
            );
            List<InGamePaintModel> list = [];

            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    list = JsonSerializer.Deserialize<List<InGamePaintModel>>(json) ?? [];
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to deserialize ingame_paints.json");
                }
            }

            string inGameDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "Paints",
                "InGame"
            );
            if (Directory.Exists(inGameDir))
            {
                string[] files =
                [
                    .. Directory
                        .GetFiles(inGameDir, "*.*")
                        .Where(f =>
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        ),
                ];

                bool modified = false;
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!list.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string relativePreview = Path.Combine(
                                "Textures",
                                "Paints",
                                "InGame",
                                Path.GetFileName(file)
                            )
                            .Replace("\\", "/");
                        string targetUrl = name.ToLower();

                        list.Add(
                            new InGamePaintModel
                            {
                                Name = name,
                                PreviewImage = relativePreview,
                                TargetUrl = targetUrl,
                            }
                        );
                        modified = true;
                    }
                }

                if (modified || !File.Exists(jsonPath))
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(list, options);
                        File.WriteAllText(jsonPath, json);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to write ingame_paints.json");
                    }
                }
            }

            return list;
        }

        private bool IsSkinMissingAssets(SkinModel skin)
        {
            if (
                string.IsNullOrEmpty(skin.PreviewImage)
                || skin.PreviewImage.StartsWith("pack:", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string previewPath = Path.GetFullPath(
                Path.Combine(baseDir, skin.PreviewImage.Replace("\\", "/"))
            );
            if (!File.Exists(previewPath))
            {
                return true;
            }

            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                string folder = Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"));
                bool found = false;
                if (Directory.Exists(folder))
                {
                    string[] allowedExts = [".png", ".jpg", ".jpeg"];
                    foreach (string ext in allowedExts)
                    {
                        if (File.Exists(Path.Combine(folder, $"{skin.Name}{ext}")))
                        {
                            found = true;
                            break;
                        }
                    }
                }
                return !found;
            }

            List<string> suffixes =
                skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase)
                || skin.Category.Equals("Spawners", StringComparison.OrdinalIgnoreCase)
                    ? (skin.Category.Equals("Spawners", StringComparison.OrdinalIgnoreCase) && skin.Name.Contains("Parachute", StringComparison.OrdinalIgnoreCase) ? ["para"] : ["details"])
                    : ["details", "lightmap"];

            if (!string.IsNullOrEmpty(skin.AlphaTarget))
            {
                suffixes.Add("alpha");
            }

            string baseFolder = Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"));
            if (!Directory.Exists(baseFolder))
            {
                return true;
            }

            string targetFolder = baseFolder;
            if (
                skin.Category is "Turrets" or "Hulls"
                && !Directory.EnumerateFiles(baseFolder, "details.*").Any()
            )
            {
                string m3Folder = Path.Combine(baseFolder, "M3");
                if (Directory.Exists(m3Folder))
                {
                    targetFolder = m3Folder;
                }
            }

            foreach (string suffix in suffixes)
            {
                bool found = false;
                if (Directory.Exists(targetFolder))
                {
                    string[] matchingFiles = Directory.GetFiles(targetFolder, $"{suffix}.*");
                    foreach (string file in matchingFiles)
                    {
                        string fname = Path.GetFileName(file);
                        if (fname.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                        string ext = Path.GetExtension(file).ToLower();
                        string[] allowed = [".png", ".jpg", ".jpeg"];
                        if (allowed.Contains(ext))
                        {
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(skin.ModelTarget))
            {
                if (
                    !File.Exists(Path.Combine(targetFolder, "object.3ds"))
                    && !File.Exists(Path.Combine(targetFolder, "object.3ds.enc"))
                    && !File.Exists(Path.Combine(baseFolder, "object.3ds"))
                    && !File.Exists(Path.Combine(baseFolder, "object.3ds.enc"))
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
