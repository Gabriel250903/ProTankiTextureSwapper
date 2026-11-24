using System.IO;
using System.Text.Json;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class EquipmentTargetService : IEquipmentTargetService
    {
        private readonly object _lock = new();
        private EquipmentTargetsCatalog _catalog = new();

        public EquipmentTargetService()
        {
            Reload();
        }

        public void Reload()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string primaryPath = Path.Combine(baseDir, Constants.EquipmentTargetsJson);
            string devPath = Path.GetFullPath(
                Path.Combine(baseDir, "..", "..", "..", Constants.EquipmentTargetsJson)
            );

            string? pathToLoad =
                File.Exists(primaryPath) ? primaryPath
                : File.Exists(devPath) ? devPath
                : null;

            EquipmentTargetsCatalog loaded = new();
            if (pathToLoad != null)
            {
                try
                {
                    string json = File.ReadAllText(pathToLoad);
                    EquipmentTargetsCatalog? parsed =
                        JsonSerializer.Deserialize<EquipmentTargetsCatalog>(json);
                    if (parsed != null)
                    {
                        loaded = NormalizeCatalog(parsed);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "Failed to load {File} from {Path}",
                        Constants.EquipmentTargetsJson,
                        pathToLoad
                    );
                }
            }
            else
            {
                Log.Warning(
                    "{File} was not found at {Primary} or {Dev}.",
                    Constants.EquipmentTargetsJson,
                    primaryPath,
                    devPath
                );
            }

            lock (_lock)
            {
                _catalog = loaded;
            }
        }

        public bool TryGetResourceId(
            string category,
            string itemName,
            string tier,
            out long resourceId
        )
        {
            resourceId = 0;
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            string normalizedTier = NormalizeTier(tier);
            lock (_lock)
            {
                Dictionary<string, Dictionary<string, EquipmentTierTarget>>? catMap =
                    category.Equals("Turrets", StringComparison.OrdinalIgnoreCase)
                        ? _catalog.Turrets
                    : category.Equals("Hulls", StringComparison.OrdinalIgnoreCase) ? _catalog.Hulls
                    : null;

                if (
                    catMap == null
                    || !catMap.TryGetValue(
                        itemName,
                        out Dictionary<string, EquipmentTierTarget>? tiers
                    )
                    || !tiers.TryGetValue(normalizedTier, out EquipmentTierTarget? target)
                    || !target.ResourceId.HasValue
                    || target.ResourceId.Value <= 0
                )
                {
                    return false;
                }

                resourceId = target.ResourceId.Value;
                return true;
            }
        }

        public int GetMappedCount(string tier)
        {
            string normalizedTier = NormalizeTier(tier);
            lock (_lock)
            {
                int count = 0;
                foreach (Dictionary<string, EquipmentTierTarget> tiers in _catalog.Turrets.Values)
                {
                    if (
                        tiers.TryGetValue(normalizedTier, out EquipmentTierTarget? target)
                        && target.ResourceId is > 0
                    )
                    {
                        count++;
                    }
                }
                foreach (Dictionary<string, EquipmentTierTarget> tiers in _catalog.Hulls.Values)
                {
                    if (
                        tiers.TryGetValue(normalizedTier, out EquipmentTierTarget? target)
                        && target.ResourceId is > 0
                    )
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int GetTotalEquipmentCount()
        {
            lock (_lock)
            {
                int total = _catalog.Turrets.Count + _catalog.Hulls.Count;
                return total > 0 ? total : 18;
            }
        }

        public bool HasAnyMappedTargets(string tier)
        {
            return GetMappedCount(tier) > 0;
        }

        public string? ResolveBaseM3ModelPath(string category, string itemName)
        {
            if (
                string.IsNullOrWhiteSpace(category)
                || string.IsNullOrWhiteSpace(itemName)
                || (
                    !category.Equals("Turrets", StringComparison.OrdinalIgnoreCase)
                    && !category.Equals("Hulls", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }

            string normalizedCategory = category.Equals(
                "Turrets",
                StringComparison.OrdinalIgnoreCase
            )
                ? "Turrets"
                : "Hulls";
            string relPath = $"Assets/BaseModels/{normalizedCategory}/{itemName}/M3/object.3ds";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            string primaryFull = Path.Combine(
                baseDir,
                relPath.Replace('/', Path.DirectorySeparatorChar)
            );
            string devFull = Path.Combine(
                devRoot,
                relPath.Replace('/', Path.DirectorySeparatorChar)
            );

            bool exists =
                File.Exists(primaryFull)
                || File.Exists(primaryFull + ".enc")
                || File.Exists(devFull)
                || File.Exists(devFull + ".enc");

            return exists ? relPath : null;
        }

        public SkinTierResolution ResolveSkinForTier(SkinModel skin, string targetTier)
        {
            string tier = NormalizeTier(targetTier);

            if (skin.Category is not ("Hulls" or "Turrets") || tier == "M3")
            {
                SkinModel m3Clone = CloneSkin(
                    skin,
                    targetTier: "M3",
                    sourceFolder: skin.SourceFolder,
                    selectedMod: skin.IsM3Available ? "M3" : skin.SelectedModification,
                    detailsTarget: skin.DetailsTarget,
                    lightmapTarget: skin.LightmapTarget,
                    alphaTarget: skin.AlphaTarget,
                    modelTarget: skin.ModelTarget,
                    tracksTarget: skin.TracksTarget,
                    imagesXmlTarget: skin.ImagesXmlTarget,
                    fallbackModelPath: null
                );

                return new SkinTierResolution(
                    SkinTierResolutionMode.NativeM3,
                    IsAvailable: true,
                    UsesM3ModelFallback: false,
                    UnavailableReason: null,
                    TargetResourceId: TryExtractResourceId(skin.DetailsTarget),
                    ResolvedSkin: m3Clone
                );
            }

            if (skin.IsXtTargetSkin)
            {
                return new SkinTierResolution(
                    SkinTierResolutionMode.Unsupported,
                    IsAvailable: false,
                    UsesM3ModelFallback: false,
                    UnavailableReason: $"'{skin.Name}' only targets the XT model and is hidden in {tier} mode.",
                    TargetResourceId: null,
                    ResolvedSkin: null
                );
            }

            if (!TryGetResourceId(skin.Category, skin.ItemName, tier, out long resId))
            {
                return new SkinTierResolution(
                    SkinTierResolutionMode.MissingTargetId,
                    IsAvailable: false,
                    UsesM3ModelFallback: false,
                    UnavailableReason: $"{skin.ItemName} {tier} target ID is not mapped yet. Equip/view {skin.ItemName} {tier} in ProTanki HTML5 and run Html5TargetScanner.",
                    TargetResourceId: null,
                    ResolvedSkin: null
                );
            }

            skin.RefreshAvailableModifications();

            if (skin.HasModificationAvailable(tier))
            {
                string nativeFolder = skin.GetModificationFolder(tier);
                bool hasNativeModel = HasModelFileInFolder(nativeFolder);
                string modelTarget = hasNativeModel
                    ? Html5ResourceMapper.EncodeHtml5Target(resId, "object.3ds")
                    : string.Empty;

                SkinModel nativeClone = CloneSkin(
                    skin,
                    targetTier: tier,
                    sourceFolder: nativeFolder,
                    selectedMod: tier,
                    detailsTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details.jpg"),
                    lightmapTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "lightmap.jpg"),
                    alphaTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details_alpha.jpg"),
                    modelTarget: modelTarget,
                    tracksTarget: string.IsNullOrEmpty(skin.TracksTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "tracks.jpg"),
                    imagesXmlTarget: string.IsNullOrEmpty(skin.ImagesXmlTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "images.xml"),
                    fallbackModelPath: null
                );

                return new SkinTierResolution(
                    SkinTierResolutionMode.NativeTier,
                    IsAvailable: true,
                    UsesM3ModelFallback: false,
                    UnavailableReason: null,
                    TargetResourceId: resId,
                    ResolvedSkin: nativeClone
                );
            }

            if (skin.HasCustomModel)
            {
                SkinModel customModelClone = CloneSkin(
                    skin,
                    targetTier: tier,
                    sourceFolder: skin.SourceFolder,
                    selectedMod: "M3",
                    detailsTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details.jpg"),
                    lightmapTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "lightmap.jpg"),
                    alphaTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details_alpha.jpg"),
                    modelTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "object.3ds"),
                    tracksTarget: string.IsNullOrEmpty(skin.TracksTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "tracks.jpg"),
                    imagesXmlTarget: string.IsNullOrEmpty(skin.ImagesXmlTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "images.xml"),
                    fallbackModelPath: null
                );

                return new SkinTierResolution(
                    SkinTierResolutionMode.CustomModelFallback,
                    IsAvailable: true,
                    UsesM3ModelFallback: true,
                    UnavailableReason: null,
                    TargetResourceId: resId,
                    ResolvedSkin: customModelClone
                );
            }

            string normalizedSource = skin.SourceFolder.Replace('\\', '/').TrimEnd('/');
            if (normalizedSource.EndsWith("/M3", StringComparison.OrdinalIgnoreCase))
            {
                string? baseModelPath = ResolveBaseM3ModelPath(skin.Category, skin.ItemName);
                if (string.IsNullOrEmpty(baseModelPath))
                {
                    return new SkinTierResolution(
                        SkinTierResolutionMode.MissingBaseModel,
                        IsAvailable: false,
                        UsesM3ModelFallback: true,
                        UnavailableReason: $"Requires vanilla {skin.ItemName} M3 model (Assets/BaseModels/{skin.Category}/{skin.ItemName}/M3/object.3ds) to apply M3 textures safely onto {skin.ItemName} {tier}. Run Html5TargetScanner --export-base-models.",
                        TargetResourceId: resId,
                        ResolvedSkin: null
                    );
                }

                SkinModel baseModelClone = CloneSkin(
                    skin,
                    targetTier: tier,
                    sourceFolder: skin.SourceFolder,
                    selectedMod: "M3",
                    detailsTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details.jpg"),
                    lightmapTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "lightmap.jpg"),
                    alphaTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "details_alpha.jpg"),
                    modelTarget: Html5ResourceMapper.EncodeHtml5Target(resId, "object.3ds"),
                    tracksTarget: string.IsNullOrEmpty(skin.TracksTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "tracks.jpg"),
                    imagesXmlTarget: string.IsNullOrEmpty(skin.ImagesXmlTarget)
                        ? string.Empty
                        : Html5ResourceMapper.EncodeHtml5Target(resId, "images.xml"),
                    fallbackModelPath: baseModelPath
                );

                return new SkinTierResolution(
                    SkinTierResolutionMode.BaseM3ModelFallback,
                    IsAvailable: true,
                    UsesM3ModelFallback: true,
                    UnavailableReason: null,
                    TargetResourceId: resId,
                    ResolvedSkin: baseModelClone
                );
            }

            return new SkinTierResolution(
                SkinTierResolutionMode.Unsupported,
                IsAvailable: false,
                UsesM3ModelFallback: false,
                UnavailableReason: $"'{skin.Name}' does not have assets for {tier}.",
                TargetResourceId: resId,
                ResolvedSkin: null
            );
        }

        private static string NormalizeTier(string? tier)
        {
            if (string.IsNullOrWhiteSpace(tier))
            {
                return "M3";
            }

            string upper = tier.Trim().ToUpperInvariant();
            return upper is "M0" or "M1" or "M2" or "M3" ? upper : "M3";
        }

        private static long? TryExtractResourceId(string? target)
        {
            return
                Html5ResourceMapper.TryDecodeBase64Target(target, out Html5ResourceTarget? decoded)
                && decoded != null
                ? decoded.ResourceId
                : null;
        }

        private static bool HasModelFileInFolder(string relativeFolder)
        {
            string clean = relativeFolder.Replace('\\', '/').TrimStart('/');
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            foreach (string root in new[] { baseDir, devRoot })
            {
                string dir = Path.Combine(root, clean.Replace('/', Path.DirectorySeparatorChar));
                if (
                    File.Exists(Path.Combine(dir, "object.3ds"))
                    || File.Exists(Path.Combine(dir, "object.3ds.enc"))
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static SkinModel CloneSkin(
            SkinModel original,
            string targetTier,
            string sourceFolder,
            string selectedMod,
            string detailsTarget,
            string lightmapTarget,
            string alphaTarget,
            string modelTarget,
            string tracksTarget,
            string imagesXmlTarget,
            string? fallbackModelPath
        )
        {
            return new SkinModel
            {
                Category = original.Category,
                SubCategory = original.SubCategory,
                ItemName = original.ItemName,
                Name = original.Name,
                Tags = [.. original.Tags],
                SourceFolder = sourceFolder,
                PreviewImage = original.PreviewImage,
                DetailsTarget = detailsTarget,
                LightmapTarget = lightmapTarget,
                AlphaTarget = alphaTarget,
                ModelTarget = modelTarget,
                TracksTarget = tracksTarget,
                ImagesXmlTarget = imagesXmlTarget,
                SecondaryDetailsTarget = original.SecondaryDetailsTarget,
                SecondaryModelTarget = original.SecondaryModelTarget,
                IsPremium = original.IsPremium,
                IsExperimental = original.IsExperimental,
                WarningMessage = original.WarningMessage,
                TargetTier = targetTier,
                FallbackModelPath = fallbackModelPath,
                SelectedModification = selectedMod,
            };
        }

        private static EquipmentTargetsCatalog NormalizeCatalog(EquipmentTargetsCatalog parsed)
        {
            EquipmentTargetsCatalog normalized = new();
            foreach (
                KeyValuePair<
                    string,
                    Dictionary<string, EquipmentTierTarget>
                > catEntry in parsed.Turrets
            )
            {
                normalized.Turrets[catEntry.Key] = new Dictionary<string, EquipmentTierTarget>(
                    catEntry.Value,
                    StringComparer.OrdinalIgnoreCase
                );
            }
            foreach (
                KeyValuePair<
                    string,
                    Dictionary<string, EquipmentTierTarget>
                > catEntry in parsed.Hulls
            )
            {
                normalized.Hulls[catEntry.Key] = new Dictionary<string, EquipmentTierTarget>(
                    catEntry.Value,
                    StringComparer.OrdinalIgnoreCase
                );
            }
            return normalized;
        }
    }
}
