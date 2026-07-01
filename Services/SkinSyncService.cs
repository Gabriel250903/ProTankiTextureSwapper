using Serilog;
using System.IO;
using System.Text.Json;
using TextureSwapper.Core;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public class SkinSyncService(IUpdateService updateService) : ISkinSyncService
    {
        private readonly IUpdateService _updateService = updateService;
        public event Action<string>? ProgressChanged;
        private readonly JsonSerializerOptions options = new() { WriteIndented = true };

        public async Task<(List<SkinModel> Skins, List<InGamePaintModel>? RemoteInGamePaints)> SyncAndLoadSkinsAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localHullsPath = Path.Combine(baseDir, Constants.HullsSkinsJson);
            string localTurretsPath = Path.Combine(baseDir, Constants.TurretsSkinsJson);
            string localSuppliesPath = Path.Combine(baseDir, Constants.SuppliesSkinsJson);
            string localPaintsPath = Path.Combine(baseDir, Constants.PaintsSkinsJson);

            (string Name, string LocalPath, string RemoteFileName)[] categories =
            [
                (Name: "Hulls", LocalPath: localHullsPath, RemoteFileName: Constants.HullsSkinsJson),
                (Name: "Turrets", LocalPath: localTurretsPath, RemoteFileName: Constants.TurretsSkinsJson),
                (Name: "Supplies", LocalPath: localSuppliesPath, RemoteFileName: Constants.SuppliesSkinsJson),
                (Name: "Paints", LocalPath: localPaintsPath, RemoteFileName: Constants.PaintsSkinsJson)
            ];

            List<SkinModel> combinedSkins = [];
            List<InGamePaintModel>? remoteInGamePaints = null;

            foreach ((string Name, string LocalPath, string RemoteFileName) in categories)
            {
                string localJson = File.Exists(LocalPath) ? await File.ReadAllTextAsync(LocalPath) : string.Empty;
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
                (List<SkinModel>? remoteSkins, string? remoteJson) = await _updateService.FetchRemoteSkinsFileAsync(RemoteFileName);

                if (remoteSkins != null && remoteJson != localJson)
                {
                    Log.Information($"Remote {RemoteFileName} is different from local. Merging changes...");
                    bool merged = false;
                    foreach (SkinModel remoteSkin in remoteSkins)
                    {
                        SkinModel? localSkin = catSkins.FirstOrDefault(s => s.Name.Equals(remoteSkin.Name, StringComparison.OrdinalIgnoreCase));
                        if (localSkin != null)
                        {
                            if (localSkin.DetailsTarget != remoteSkin.DetailsTarget ||
                                localSkin.LightmapTarget != remoteSkin.LightmapTarget ||
                                localSkin.AlphaTarget != remoteSkin.AlphaTarget ||
                                localSkin.ModelTarget != remoteSkin.ModelTarget ||
                                localSkin.SourceFolder != remoteSkin.SourceFolder ||
                                localSkin.PreviewImage != remoteSkin.PreviewImage)
                            {
                                localSkin.DetailsTarget = remoteSkin.DetailsTarget;
                                localSkin.LightmapTarget = remoteSkin.LightmapTarget;
                                localSkin.AlphaTarget = remoteSkin.AlphaTarget;
                                localSkin.ModelTarget = remoteSkin.ModelTarget;
                                localSkin.SourceFolder = remoteSkin.SourceFolder;
                                localSkin.PreviewImage = remoteSkin.PreviewImage;
                                merged = true;
                            }
                        }
                        else
                        {
                            catSkins.Add(remoteSkin);
                            merged = true;
                        }
                    }

                    HashSet<string> remoteNames = new(remoteSkins.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
                    int removed = catSkins.RemoveAll(s => !remoteNames.Contains(s.Name));
                    if (removed > 0)
                    {
                        merged = true;
                    }

                    if (merged)
                    {
                        string mergedJson = JsonSerializer.Serialize(catSkins, options);
                        await File.WriteAllTextAsync(LocalPath, mergedJson);
                    }
                }

                List<SkinModel> missing = [.. catSkins.Where(IsSkinMissingAssets)];
                if (missing.Count > 0 && !_updateService.IsOffline)
                {
                    int completed = 0;
                    object progressLock = new();
                    using SemaphoreSlim semaphore = new(5);
                    List<Task> downloadTasks = [];

                    foreach (SkinModel skin in missing)
                    {
                        downloadTasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                lock (progressLock)
                                {
                                    ProgressChanged?.Invoke($"Syncing {Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name}...");
                                }

                                await _updateService.EnsureAssetsExistAsync(skin, p =>
                                {
                                    lock (progressLock)
                                    {
                                        ProgressChanged?.Invoke($"Syncing {Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name} - {p}");
                                    }
                                });

                                skin.NotifyPreviewChanged();
                            }
                            finally
                            {
                                int currentCompleted = Interlocked.Increment(ref completed);
                                lock (progressLock)
                                {
                                    ProgressChanged?.Invoke($"Syncing {Name.ToLower()} ({currentCompleted}/{missing.Count})...");
                                }
                                _ = semaphore.Release();
                            }
                        }));
                    }

                    await Task.WhenAll(downloadTasks);
                }

                combinedSkins.AddRange(catSkins);
            }

            try
            {
                string localInGamePath = Path.Combine(baseDir, Constants.InGamePaintsJson);
                string localInGameJson = File.Exists(localInGamePath) ? await File.ReadAllTextAsync(localInGamePath) : string.Empty;
                ProgressChanged?.Invoke("Syncing in-game paints database...");
                (List<InGamePaintModel>? fetchedPaints, string? remoteInGameJson) = await _updateService.FetchRemoteInGamePaintsFileAsync(Constants.InGamePaintsJson);
                if (fetchedPaints != null && remoteInGameJson != localInGameJson)
                {
                    Log.Information("Remote ingame_paints.json is different from local. Updating...");
                    await File.WriteAllTextAsync(localInGamePath, remoteInGameJson!);
                    remoteInGamePaints = fetchedPaints;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to sync remote ingame_paints.json");
            }

            return (combinedSkins, remoteInGamePaints);
        }

        public List<BackupModel> LoadBackups()
        {
            string backupsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir);
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
                backups.Add(new BackupModel
                {
                    FolderName = fi.Name,
                    DisplayName = nameWithoutExt.Replace("_", " "),
                    CreationDate = fi.CreationTime,
                    FullPath = fi.FullName
                });
            }

            return [.. backups.OrderByDescending(b => b.CreationDate)];
        }

        public List<InGamePaintModel> LoadInGamePaints()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ingame_paints.json");
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

            string inGameDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", "Paints", "InGame");
            if (Directory.Exists(inGameDir))
            {
                string[] files = [.. Directory.GetFiles(inGameDir, "*.*")
                    .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))];

                bool modified = false;
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!list.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string relativePreview = Path.Combine("Textures", "Paints", "InGame", Path.GetFileName(file)).Replace("\\", "/");
                        string targetUrl = name.ToLower();

                        list.Add(new InGamePaintModel
                        {
                            Name = name,
                            PreviewImage = relativePreview,
                            TargetUrl = targetUrl
                        });
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
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string previewPath = Path.GetFullPath(Path.Combine(baseDir, skin.PreviewImage.Replace("\\", "/")));
            if (!File.Exists(previewPath))
            {
                return true;
            }

            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                string paintFile = Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"), $"{skin.Name}.png");
                return !File.Exists(paintFile);
            }

            string[] suffixes = skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase)
                ? ["details"]
                : ["details", "lightmap", "alpha"];

            foreach (string suffix in suffixes)
            {
                string folder = Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"));
                bool found = false;
                if (Directory.Exists(folder))
                {
                    string[] matchingFiles = Directory.GetFiles(folder, $"{suffix}.*");
                    foreach (string file in matchingFiles)
                    {
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

            return false;
        }
    }
}
