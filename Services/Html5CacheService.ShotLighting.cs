using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService
    {
        private const string ShotLightBattleMarker = "/*__TS_SHOT_LIGHT_V2__*/";
        private const string LegacyShotLightBattleMarkerV1 = "/*__TS_SHOT_LIGHT_V1__*/";
        private const string ShotLightCoreMarker = "/*__TS_SHOT_LIGHT_CORE_V1__*/";
        private const string ShotLightCfgStartMarker = "/*__TS_SHOT_LIGHT_CFG_START_V1__*/";
        private const string ShotLightCfgEndMarker = "/*__TS_SHOT_LIGHT_CFG_END_V1__*/";
        private const string ShotLightP986Start = "/*__TS_SL_P986_S__*/";
        private const string ShotLightP986End = "/*__TS_SL_P986_E__*/";
        private const string ShotLightMpStart = "/*__TS_SL_MP_S__*/";
        private const string ShotLightMpEnd = "/*__TS_SL_MP_E__*/";
        private const string ShotLightBcshStart = "/*__TS_SL_BCSH_S__*/";
        private const string ShotLightBcshEnd = "/*__TS_SL_BCSH_E__*/";

        private const string BkGroundOrig = @"base\x20+\x20albedo\x20*\x20flashDynamicLight()";
        private const string BkGroundPatched =
            @"max(\x20base\x20+\x20albedo\x20*\x20flashDynamicLight(),\x20vec3(\x200.0\x20)\x20)";
        private const string WkSpriteOrig =
            @"spriteBase\x20+\x20spriteAlbedo\x20*\x20flashDynamicLight()";
        private const string WkSpritePatched =
            @"max(\x20spriteBase\x20+\x20spriteAlbedo\x20*\x20flashDynamicLight(),\x20vec3(\x200.0\x20)\x20)";
        private const string UvLimitOrig = @"'\x20\x20float\x20dynLightLimit\x20=\x20(\x20min(";
        private const string UvLimitPatched =
            @"'\x20\x20float\x20dynLightLimit\x20=\x20dynLightTotal\x20<\x200.0\x20?\x201.0\x20:\x20(\x20min(";
        private const string UvOutOrig =
            @"'\x20\x20gl_FragColor.rgb\x20=\x20pow(\x20clamp(\x20dynLightOut,\x200.0,\x201.0\x20),\x20vec3(\x202.2\x20)\x20);'";
        private const string UvOutPatched =
            @"'\x20\x20gl_FragColor.rgb\x20=\x20pow(\x20clamp(\x20dynLightTotal\x20<\x200.0\x20?\x20dynLightBase\x20*\x20exp(\x20max(\x20dynLightTotal,\x20-4.0\x20)\x20*\x200.45\x20)\x20:\x20dynLightOut,\x200.0,\x201.0\x20),\x20vec3(\x202.2\x20)\x20);'";

        [GeneratedRegex(
            @"function\s+(?<fn>[A-Za-z0-9_$]+)\((?<arg>[A-Za-z0-9_$]+)\)\{return\s+(?<ft>[A-Za-z0-9_$]+)\(\k<arg>>>0x10&0xff,\k<arg>>>0x8&0xff,\k<arg>&0xff\);\}"
        )]
        private static partial Regex ShotLightFrOrigRegex();

        [GeneratedRegex(
            @"function\s+(?<fn>[A-Za-z0-9_$]+)\((?<arg>[A-Za-z0-9_$]+)\)\{if\(\k<arg><0\)return\{'r':-1,'g':-1,'b':-1\};return\s+(?<ft>[A-Za-z0-9_$]+)\(\k<arg>>>0x10&0xff,\k<arg>>>0x8&0xff,\k<arg>&0xff\);\}"
        )]
        private static partial Regex ShotLightFrPatchedRegex();

        [GeneratedRegex(
            @"\['(?<fn>[A-Za-z0-9_$]+)'\]\((?<kind>[A-Za-z0-9_$]+),(?<mod>[A-Za-z0-9_$]+),(?<lit>[A-Za-z0-9_$]+)\)\{for\(let\s+(?<eff>[A-Za-z0-9_$]+)\s+of\s+\k<lit>\?\.\['effects'\]\?\?\[\]\)\{"
        )]
        private static partial Regex ShotLightP986OrigRegex();

        [GeneratedRegex(
            @"let\s+(?<sfxVar>[A-Za-z0-9_$]+)=(?<dataVar>[A-Za-z0-9_$]+)\?(?<turretEntry>[A-Za-z0-9_$]+)\['sfx'\]\(\k<dataVar>\):null;this\['lightSources'\]\['[A-Za-z0-9_$]+'\]\((?<kindVar>[A-Za-z0-9_$]+),"
        )]
        private static partial Regex ShotLightZySfxOrigRegex();

        [GeneratedRegex(
            @"(?<a>[A-Za-z0-9_$]+)\['color'\]>>>(?<shift>[A-Za-z0-9_$]+)&0xff\)\/0xff,\((?<b>[A-Za-z0-9_$]+)\['color'\]>>>\k<shift>&0xff\)\/0xff\)\*0xff\)\/0xff;return\{'intensity':(?<interp>[A-Za-z0-9_$]+)\(\k<a>\['intensity'\],\k<b>\['intensity'\]\),'attenuationBegin':\k<interp>\(\k<a>\['attenuationBegin'\],\k<b>\['attenuationBegin'\]\),'attenuationEnd':\k<interp>\(\k<a>\['attenuationEnd'\],\k<b>\['attenuationEnd'\]\),'color':\{'r':(?<colFn>[A-Za-z0-9_$]+)\(0x10\),'g':\k<colFn>\(0x8\),'b':\k<colFn>\(0x0\)\}\};"
        )]
        private static partial Regex ShotLightMpOrigRegex();

        [GeneratedRegex(
            @"let\s+\{colors:(?<col>[A-Za-z0-9_$]+),ranges:(?<rng>[A-Za-z0-9_$]+),types:(?<typ>[A-Za-z0-9_$]+)\}=(?<obj>[A-Za-z0-9_$]+),(?<cidx>[A-Za-z0-9_$]+)=(?<lidx>[A-Za-z0-9_$]+)\*0x3,(?<lum>[A-Za-z0-9_$]+)=Math\['max'\]\(\k<col>\[\k<cidx>\],\k<col>\[\k<cidx>\+0x1\],\k<col>\[\k<cidx>\+0x2\]\);"
        )]
        private static partial Regex ShotLightJtOrigRegex();

        [GeneratedRegex(
            @"Math\['max'\]\(Math\['abs'\]\((?<col>[A-Za-z0-9_$]+)\[(?<cidx>[A-Za-z0-9_$]+)\]\),Math\['abs'\]\(\k<col>\[\k<cidx>\+0x1\]\),Math\['abs'\]\(\k<col>\[\k<cidx>\+0x2\]\)\)"
        )]
        private static partial Regex ShotLightJtPatchedRegex();

        [GeneratedRegex(
            @"function\s+(?<fn>[A-Za-z0-9_$]+)\((?<arg1>[A-Za-z0-9_$]+),(?<arg2>[A-Za-z0-9_$]+)\)\{let\s+(?<idx>[A-Za-z0-9_$]+)=\k<arg2>\*0x3;return\s+(?<weights>[A-Za-z0-9_$]+)\[0x0\]\*\k<arg1>\['colors'\]\[\k<idx>\]\+\k<weights>\[0x1\]\*\k<arg1>\['colors'\]\[\k<idx>\+0x1\]\+\k<weights>\[0x2\]\*\k<arg1>\['colors'\]\[\k<idx>\+0x2\];\}"
        )]
        private static partial Regex ShotLightFmOrigRegex();

        [GeneratedRegex(
            @"function\s+(?<fn>[A-Za-z0-9_$]+)\((?<arg1>[A-Za-z0-9_$]+),(?<arg2>[A-Za-z0-9_$]+)\)\{let\s+(?<idx>[A-Za-z0-9_$]+)=\k<arg2>\*0x3;return\s+/\*__TS_SHOT_LIGHT_CORE_V1__\*/Math\['abs'\]\((?<weights>[A-Za-z0-9_$]+)\[0x0\]\*\k<arg1>\['colors'\]\[\k<idx>\]\+\k<weights>\[0x1\]\*\k<arg1>\['colors'\]\[\k<idx>\+0x1\]\+\k<weights>\[0x2\]\*\k<arg1>\['colors'\]\[\k<idx>\+0x2\]\);\}"
        )]
        private static partial Regex ShotLightFmPatchedRegex();

        public void SyncShotDynamicLighting(string cacheRootPath)
        {
            if (string.IsNullOrWhiteSpace(cacheRootPath))
            {
                return;
            }

            if (!TryResolveProTankiRoot(cacheRootPath, out string rootDir))
            {
                return;
            }

            string cacheDataDir = Path.Combine(rootDir, "Cache", "Cache_Data");
            if (!Directory.Exists(cacheDataDir))
            {
                return;
            }

            try
            {
                EnsureCacheUnlocked(cacheDataDir);
            }
            catch (IOException ex)
            {
                Log.Warning(
                    ex,
                    "Shot Dynamic Lighting sync skipped because ProTanki cache is locked."
                );
                RecordAndSavePatchReport(
                    cacheDataDir,
                    [
                        new BundlePatchDiagnostic
                        {
                            FileName = "(shot-lighting)",
                            BundleKind = "Battle/Core",
                            Feature = "ShotLighting",
                            Status = "Failed",
                            Reason = ex.Message,
                        },
                    ],
                    replaceAll: false
                );
                return;
            }

            ISettingsService? svc =
                settingsService
                ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService;
            AppSettings settings = svc?.Load() ?? new AppSettings();

            string cfgJson = BuildShotLightConfigJson(
                settings,
                out bool shouldPatchBattle,
                out bool shouldPatchCore
            );
            string cfgBlock =
                $"{ShotLightCfgStartMarker}\n;globalThis['__tsShotLightCfg']={cfgJson};\n{ShotLightCfgEndMarker}";

            string originalsCacheDataDir = GetOriginalsCacheDataDir();

            _ = EnsureCacheDataBundlesIndexed(cacheDataDir);
            List<string> battlePaths =
            [
                .. _bundleIndexCache
                    .Where(kv => kv.Value.IsBattleLighting && File.Exists(kv.Key))
                    .Select(kv => kv.Key),
            ];
            List<string> corePaths =
            [
                .. _bundleIndexCache
                    .Where(kv => kv.Value.IsCore && File.Exists(kv.Key))
                    .Select(kv => kv.Key),
            ];

            bool anyModified = false;
            int battleSucceeded = 0;
            List<string> skippedBattleFiles = [];
            List<BundlePatchDiagnostic> diagnostics = [];

            foreach (string battlePath in battlePaths)
            {
                if (!TryReadZstdBundle(battlePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                string fileName = Path.GetFileName(battlePath);
                if (shouldPatchBattle)
                {
                    string bodyWithoutCfg = RemoveMarkerSpan(
                            jsText,
                            ShotLightCfgStartMarker,
                            ShotLightCfgEndMarker
                        )
                        .TrimEnd('\r', '\n');

                    bool hasLegacyV1 = bodyWithoutCfg.Contains(
                        LegacyShotLightBattleMarkerV1,
                        StringComparison.Ordinal
                    );
                    string sourceBody = bodyWithoutCfg;
                    string fingerprint = ComputeBuildFingerprint(sourceBody);
                    if (hasLegacyV1)
                    {
                        sourceBody = RemoveBattleShotLightingPatch(
                            bodyWithoutCfg,
                            out bool migrated
                        );
                        if (!migrated)
                        {
                            Log.Warning(
                                "Could not migrate legacy HTML5 Shot Dynamic Lighting patch in bundle {File}.",
                                fileName
                            );
                            diagnostics.Add(
                                new BundlePatchDiagnostic
                                {
                                    FileName = fileName,
                                    BundleKind = "Battle",
                                    Feature = "ShotLighting",
                                    Status = "Skipped",
                                    BuildFingerprint = fingerprint,
                                    Reason = "Legacy ShotLighting V1 migration failed.",
                                }
                            );
                            continue;
                        }
                    }

                    bool hasPatch = sourceBody.Contains(
                        ShotLightBattleMarker,
                        StringComparison.Ordinal
                    );
                    bool ok = true;
                    List<string> missingSigs = [];
                    string patchedBody = hasPatch
                        ? sourceBody
                        : ApplyBattleShotLightingPatch(sourceBody, out ok, missingSigs);
                    if (!ok)
                    {
                        skippedBattleFiles.Add(fileName);
                        diagnostics.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "Battle",
                                Feature = "ShotLighting",
                                Status = "Skipped",
                                BuildFingerprint = fingerprint,
                                Reason = "ShotLighting battle signatures did not match.",
                                MissingSignatures = missingSigs,
                            }
                        );
                        continue;
                    }

                    battleSucceeded++;
                    string finalJs = patchedBody + "\n" + cfgBlock;
                    if (string.Equals(jsText, finalJs, StringComparison.Ordinal))
                    {
                        diagnostics.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "Battle",
                                Feature = "ShotLighting",
                                Status = "AlreadyPatched",
                                BuildFingerprint = fingerprint,
                            }
                        );
                        continue;
                    }

                    WritePatchedZstdBundle(
                        cacheDataDir,
                        originalsCacheDataDir,
                        battlePath,
                        fileName,
                        rawBytes,
                        rawBytes.Length,
                        finalJs,
                        saveBackup: !hasPatch
                            && !hasLegacyV1
                            && !HasOtherCustomPatchesForShotLight(sourceBody),
                        patchName: "ShotLighting",
                        cleanOriginalJsForFingerprint: sourceBody
                    );
                    anyModified = true;
                    diagnostics.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Battle",
                            Feature = "ShotLighting",
                            Status = "Patched",
                            BuildFingerprint = fingerprint,
                        }
                    );
                    Log.Information(
                        "Synced HTML5 Shot Dynamic Lighting in Battle bundle {File}.",
                        fileName
                    );
                }
                else
                {
                    bool hasAnyShotLight =
                        jsText.Contains(ShotLightBattleMarker, StringComparison.Ordinal)
                        || jsText.Contains(LegacyShotLightBattleMarkerV1, StringComparison.Ordinal)
                        || jsText.Contains(ShotLightCfgStartMarker, StringComparison.Ordinal);
                    if (!hasAnyShotLight)
                    {
                        continue;
                    }

                    string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                    if (
                        !HasOtherCustomPatchesForShotLight(jsText)
                        && File.Exists(backupFile)
                        && TryRestoreCleanOriginalBundle(
                            cacheDataDir,
                            battlePath,
                            fileName,
                            rawBytes.Length,
                            backupFile
                        )
                    )
                    {
                        anyModified = true;
                        Log.Information(
                            "Restored original HTML5 Battle bundle {File} for Shot Dynamic Lighting.",
                            fileName
                        );
                        continue;
                    }

                    string stripped = RemoveMarkerSpan(
                            jsText,
                            ShotLightCfgStartMarker,
                            ShotLightCfgEndMarker
                        )
                        .TrimEnd('\r', '\n');
                    bool hasBattleMarker =
                        stripped.Contains(ShotLightBattleMarker, StringComparison.Ordinal)
                        || stripped.Contains(
                            LegacyShotLightBattleMarkerV1,
                            StringComparison.Ordinal
                        );
                    bool ok = true;
                    string reverted = hasBattleMarker
                        ? RemoveBattleShotLightingPatch(stripped, out ok)
                        : stripped;
                    if (!ok)
                    {
                        Log.Warning(
                            "Could not cleanly remove HTML5 Shot Dynamic Lighting patch from {File}.",
                            fileName
                        );
                        continue;
                    }

                    if (!string.Equals(jsText, reverted, StringComparison.Ordinal))
                    {
                        WritePatchedZstdBundle(
                            cacheDataDir,
                            originalsCacheDataDir,
                            battlePath,
                            fileName,
                            rawBytes,
                            rawBytes.Length,
                            reverted,
                            saveBackup: false
                        );
                        anyModified = true;
                        Log.Information(
                            "Removed HTML5 Shot Dynamic Lighting patch from Battle bundle {File}.",
                            fileName
                        );
                    }
                }
            }

            if (skippedBattleFiles.Count > 0)
            {
                string skippedList = string.Join(", ", skippedBattleFiles);
                if (battleSucceeded > 0)
                {
                    Log.Debug(
                        "Skipped legacy/unmatched HTML5 Shot Dynamic Lighting bundle(s): {Files}.",
                        skippedList
                    );
                }
                else
                {
                    Log.Warning(
                        "Could not safely apply HTML5 Shot Dynamic Lighting patch in bundle(s): {Files}.",
                        skippedList
                    );
                }
            }

            foreach (string corePath in corePaths)
            {
                if (!TryReadZstdBundle(corePath, out byte[] rawBytes, out string jsText))
                {
                    continue;
                }

                string fileName = Path.GetFileName(corePath);
                string fingerprint = ComputeBuildFingerprint(jsText);
                bool hasCorePatch = jsText.Contains(ShotLightCoreMarker, StringComparison.Ordinal);

                if (shouldPatchCore)
                {
                    if (hasCorePatch)
                    {
                        diagnostics.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "Core",
                                Feature = "ShotLightingCore",
                                Status = "AlreadyPatched",
                                BuildFingerprint = fingerprint,
                            }
                        );
                        continue;
                    }

                    List<string> missingCoreSigs = [];
                    string patchedCore = ApplyCoreShotLightingPatch(
                        jsText,
                        out bool ok,
                        missingCoreSigs
                    );
                    if (!ok)
                    {
                        Log.Warning(
                            "Could not safely apply HTML5 Shot Dynamic Lighting Core patch in bundle {File}.",
                            fileName
                        );
                        diagnostics.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "Core",
                                Feature = "ShotLightingCore",
                                Status = "Skipped",
                                BuildFingerprint = fingerprint,
                                Reason = "ShotLighting core signature did not match.",
                                MissingSignatures = missingCoreSigs,
                            }
                        );
                        continue;
                    }

                    WritePatchedZstdBundle(
                        cacheDataDir,
                        originalsCacheDataDir,
                        corePath,
                        fileName,
                        rawBytes,
                        rawBytes.Length,
                        patchedCore,
                        saveBackup: !HasOtherCustomPatchesForShotLight(jsText),
                        patchName: "ShotLightingCore",
                        cleanOriginalJsForFingerprint: jsText
                    );
                    anyModified = true;
                    diagnostics.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Core",
                            Feature = "ShotLightingCore",
                            Status = "Patched",
                            BuildFingerprint = fingerprint,
                        }
                    );
                    Log.Information(
                        "Applied HTML5 Shot Dynamic Lighting Core patch to {File}.",
                        fileName
                    );
                }
                else if (hasCorePatch)
                {
                    string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                    if (
                        !HasOtherCustomPatchesForShotLight(jsText)
                        && File.Exists(backupFile)
                        && TryRestoreCleanOriginalBundle(
                            cacheDataDir,
                            corePath,
                            fileName,
                            rawBytes.Length,
                            backupFile
                        )
                    )
                    {
                        anyModified = true;
                        Log.Information(
                            "Restored original HTML5 Core bundle {File} for Shot Dynamic Lighting.",
                            fileName
                        );
                        continue;
                    }

                    string revertedCore = RemoveCoreShotLightingPatch(jsText, out bool ok);
                    if (!ok)
                    {
                        Log.Warning(
                            "Could not cleanly remove HTML5 Shot Dynamic Lighting Core patch from {File}.",
                            fileName
                        );
                        continue;
                    }

                    WritePatchedZstdBundle(
                        cacheDataDir,
                        originalsCacheDataDir,
                        corePath,
                        fileName,
                        rawBytes,
                        rawBytes.Length,
                        revertedCore,
                        saveBackup: false
                    );
                    anyModified = true;
                    Log.Information(
                        "Removed HTML5 Shot Dynamic Lighting Core patch from {File}.",
                        fileName
                    );
                }
            }

            if (diagnostics.Count > 0)
            {
                RecordAndSavePatchReport(cacheDataDir, diagnostics, replaceAll: false);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        internal static string BuildShotLightConfigJson(
            AppSettings settings,
            out bool shouldPatchBattle,
            out bool shouldPatchCore
        )
        {
            bool noGlow = string.Equals(
                settings.ShotDynamicLightingMode,
                "NoGlow",
                StringComparison.OrdinalIgnoreCase
            );

            Dictionary<string, Dictionary<string, int>> map = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string kind, Html5TurretLightOverride ov) in settings.ActiveHtml5ShotLighting)
            {
                if (
                    string.IsNullOrWhiteSpace(kind)
                    || !ShotLightingColorHelper.TryParseColorInt(
                        ov.ColorHex,
                        ov.IsDark,
                        out int primary
                    )
                )
                {
                    continue;
                }

                Dictionary<string, int> entry = new() { ["p"] = primary };
                if (
                    (!string.IsNullOrWhiteSpace(ov.SecondaryColorHex) || ov.IsSecondaryDark)
                    && ShotLightingColorHelper.TryParseColorInt(
                        ov.SecondaryColorHex,
                        ov.IsSecondaryDark,
                        out int secondary
                    )
                )
                {
                    entry["s"] = secondary;
                }

                map[kind] = entry;
            }

            shouldPatchBattle = noGlow || map.Count > 0;
            shouldPatchCore = shouldPatchBattle;

            return JsonSerializer.Serialize(new { noGlow, map });
        }

        internal static string ApplyBattleShotLightingPatch(
            string jsText,
            out bool ok,
            List<string>? missingSignatures = null
        )
        {
            ok = false;

            bool matchFr = ShotLightFrOrigRegex().Matches(jsText).Count == 1;
            bool matchP986 = ShotLightP986OrigRegex().Matches(jsText).Count == 1;
            bool matchZy = ShotLightZySfxOrigRegex().Matches(jsText).Count == 1;
            bool matchMp = ShotLightMpOrigRegex().Matches(jsText).Count == 1;
            bool matchJt = ShotLightJtOrigRegex().Matches(jsText).Count == 1;
            bool matchBk = CountOccurrences(jsText, BkGroundOrig) == 1;
            bool matchWk = CountOccurrences(jsText, WkSpriteOrig) == 1;
            bool matchUvLim = CountOccurrences(jsText, UvLimitOrig) == 1;
            bool matchUvOut = CountOccurrences(jsText, UvOutOrig) == 1;

            if (
                !matchFr
                || !matchP986
                || !matchZy
                || !matchMp
                || !matchJt
                || !matchBk
                || !matchWk
                || !matchUvLim
                || !matchUvOut
            )
            {
                if (missingSignatures != null)
                {
                    if (!matchFr)
                        missingSignatures.Add("ShotLightFrOrig");
                    if (!matchP986)
                        missingSignatures.Add("ShotLightP986Orig");
                    if (!matchZy)
                        missingSignatures.Add("ShotLightZySfxOrig");
                    if (!matchMp)
                        missingSignatures.Add("ShotLightMpOrig");
                    if (!matchJt)
                        missingSignatures.Add("ShotLightJtOrig");
                    if (!matchBk)
                        missingSignatures.Add("BkGroundOrig");
                    if (!matchWk)
                        missingSignatures.Add("WkSpriteOrig");
                    if (!matchUvLim)
                        missingSignatures.Add("UvLimitOrig");
                    if (!matchUvOut)
                        missingSignatures.Add("UvOutOrig");
                }
                return jsText;
            }

            string result = ShotLightFrOrigRegex()
                .Replace(
                    jsText,
                    m =>
                        $"function {m.Groups["fn"].Value}({m.Groups["arg"].Value}){{if({m.Groups["arg"].Value}<0)return{{'r':-1,'g':-1,'b':-1}};return {m.Groups["ft"].Value}({m.Groups["arg"].Value}>>0x10&0xff,{m.Groups["arg"].Value}>>0x8&0xff,{m.Groups["arg"].Value}&0xff);}}",
                    1
                );

            result = ShotLightZySfxOrigRegex()
                .Replace(
                    result,
                    m =>
                    {
                        string dataVar = m.Groups["dataVar"].Value;
                        string kindVar = m.Groups["kindVar"].Value;
                        string inject =
                            $"{ShotLightBcshStart}if({dataVar}&&globalThis['__tsShotLightCfg']?.['map']?.[{kindVar}])"
                            + $"{{{dataVar}=Object['assign']({{}},{dataVar},{{'bcsh':null}});"
                            + $"if({dataVar}['flamethrower']){dataVar}['flamethrower']=Object['assign']({{}},{dataVar}['flamethrower'],{{'colorTransform':[]}});}}"
                            + $"{ShotLightBcshEnd}";
                        return inject + m.Value;
                    },
                    1
                );

            result = ShotLightP986OrigRegex()
                .Replace(
                    result,
                    m =>
                    {
                        string fn = m.Groups["fn"].Value;
                        string kind = m.Groups["kind"].Value;
                        string mod = m.Groups["mod"].Value;
                        string lit = m.Groups["lit"].Value;
                        string eff = m.Groups["eff"].Value;
                        string inject =
                            $"{ShotLightP986Start}{ShotLightBattleMarker}"
                            + $"var __tsC=globalThis['__tsShotLightCfg'];if(__tsC){{if(__tsC['noGlow'])return;"
                            + $"var __tsO=__tsC['map']&&__tsC['map'][{kind}];"
                            + $"if(__tsO){lit}=Object['assign']({{}},{lit},{{'effects':({lit}?.['effects']??[])['map'](function(__tsE){{"
                            + $"if(!__tsE)return __tsE;"
                            + $"var __tsCol=(__tsE['name']==='friendBeam'||__tsE['name']==='friendLoop')&&__tsO['s']!==undefined?__tsO['s']:__tsO['p'];"
                            + $"return __tsCol===undefined?__tsE:Object['assign']({{}},__tsE,{{'records':(__tsE['records']??[])['map'](function(__tsR){{return __tsR?Object['assign']({{}},__tsR,{{'color':__tsCol}}):__tsR;}})}});"
                            + $"}})}});}}{ShotLightP986End}";
                        return $"['{fn}']({kind},{mod},{lit}){{{inject}for(let {eff} of {lit}?.['effects']??[]){{";
                    },
                    1
                );

            result = ShotLightMpOrigRegex()
                .Replace(
                    result,
                    m =>
                    {
                        string a = m.Groups["a"].Value;
                        string b = m.Groups["b"].Value;
                        string colFn = m.Groups["colFn"].Value;
                        string origColorProp =
                            $"'color':{{'r':{colFn}(0x10),'g':{colFn}(0x8),'b':{colFn}(0x0)}}";
                        string patchedColorProp =
                            $"'color':{ShotLightMpStart}({a}['color']<0||{b}['color']<0)?{{'r':-1,'g':-1,'b':-1}}:{ShotLightMpEnd}{{'r':{colFn}(0x10),'g':{colFn}(0x8),'b':{colFn}(0x0)}}";
                        return m.Value.Replace(
                            origColorProp,
                            patchedColorProp,
                            StringComparison.Ordinal
                        );
                    },
                    1
                );

            result = ShotLightJtOrigRegex()
                .Replace(
                    result,
                    m =>
                    {
                        string col = m.Groups["col"].Value;
                        string cidx = m.Groups["cidx"].Value;
                        string origMax =
                            $"Math['max']({col}[{cidx}],{col}[{cidx}+0x1],{col}[{cidx}+0x2])";
                        string patchedMax =
                            $"Math['max'](Math['abs']({col}[{cidx}]),Math['abs']({col}[{cidx}+0x1]),Math['abs']({col}[{cidx}+0x2]))";
                        return m.Value.Replace(origMax, patchedMax, StringComparison.Ordinal);
                    },
                    1
                );

            result = result
                .Replace(BkGroundOrig, BkGroundPatched, StringComparison.Ordinal)
                .Replace(WkSpriteOrig, WkSpritePatched, StringComparison.Ordinal)
                .Replace(UvLimitOrig, UvLimitPatched, StringComparison.Ordinal)
                .Replace(UvOutOrig, UvOutPatched, StringComparison.Ordinal);

            ok = true;
            return result;
        }

        internal static string RemoveBattleShotLightingPatch(string jsText, out bool ok)
        {
            ok = false;
            bool hasV2 = jsText.Contains(ShotLightBattleMarker, StringComparison.Ordinal);

            if (
                ShotLightFrPatchedRegex().Matches(jsText).Count != 1
                || !jsText.Contains(ShotLightP986Start, StringComparison.Ordinal)
                || (hasV2 && !jsText.Contains(ShotLightBcshStart, StringComparison.Ordinal))
                || !jsText.Contains(ShotLightMpStart, StringComparison.Ordinal)
                || ShotLightJtPatchedRegex().Matches(jsText).Count != 1
                || CountOccurrences(jsText, BkGroundPatched) != 1
                || CountOccurrences(jsText, WkSpritePatched) != 1
                || CountOccurrences(jsText, UvLimitPatched) != 1
                || CountOccurrences(jsText, UvOutPatched) != 1
            )
            {
                return jsText;
            }

            string result = RemoveMarkerSpan(
                jsText,
                ShotLightCfgStartMarker,
                ShotLightCfgEndMarker
            );
            result = RemoveMarkerSpan(result, ShotLightP986Start, ShotLightP986End);
            result = RemoveMarkerSpan(result, ShotLightBcshStart, ShotLightBcshEnd);
            result = RemoveMarkerSpan(result, ShotLightMpStart, ShotLightMpEnd);

            result = ShotLightFrPatchedRegex()
                .Replace(
                    result,
                    m =>
                        $"function {m.Groups["fn"].Value}({m.Groups["arg"].Value}){{return {m.Groups["ft"].Value}({m.Groups["arg"].Value}>>0x10&0xff,{m.Groups["arg"].Value}>>0x8&0xff,{m.Groups["arg"].Value}&0xff);}}",
                    1
                );

            result = ShotLightJtPatchedRegex()
                .Replace(
                    result,
                    m =>
                    {
                        string col = m.Groups["col"].Value;
                        string cidx = m.Groups["cidx"].Value;
                        return $"Math['max']({col}[{cidx}],{col}[{cidx}+0x1],{col}[{cidx}+0x2])";
                    },
                    1
                );

            result = result
                .Replace(BkGroundPatched, BkGroundOrig, StringComparison.Ordinal)
                .Replace(WkSpritePatched, WkSpriteOrig, StringComparison.Ordinal)
                .Replace(UvLimitPatched, UvLimitOrig, StringComparison.Ordinal)
                .Replace(UvOutPatched, UvOutOrig, StringComparison.Ordinal);

            ok = true;
            return result;
        }

        internal static string ApplyCoreShotLightingPatch(
            string jsText,
            out bool ok,
            List<string>? missingSignatures = null
        )
        {
            ok = false;
            if (ShotLightFmOrigRegex().Matches(jsText).Count != 1)
            {
                missingSignatures?.Add("ShotLightFmOrig");
                return jsText;
            }

            string result = ShotLightFmOrigRegex()
                .Replace(
                    jsText,
                    m =>
                        $"function {m.Groups["fn"].Value}({m.Groups["arg1"].Value},{m.Groups["arg2"].Value}){{let {m.Groups["idx"].Value}={m.Groups["arg2"].Value}*0x3;return {ShotLightCoreMarker}Math['abs']({m.Groups["weights"].Value}[0x0]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}]+{m.Groups["weights"].Value}[0x1]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}+0x1]+{m.Groups["weights"].Value}[0x2]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}+0x2]);}}",
                    1
                );
            ok = true;
            return result;
        }

        internal static string RemoveCoreShotLightingPatch(string jsText, out bool ok)
        {
            ok = false;
            if (ShotLightFmPatchedRegex().Matches(jsText).Count != 1)
            {
                return jsText;
            }

            string result = ShotLightFmPatchedRegex()
                .Replace(
                    jsText,
                    m =>
                        $"function {m.Groups["fn"].Value}({m.Groups["arg1"].Value},{m.Groups["arg2"].Value}){{let {m.Groups["idx"].Value}={m.Groups["arg2"].Value}*0x3;return {m.Groups["weights"].Value}[0x0]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}]+{m.Groups["weights"].Value}[0x1]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}+0x1]+{m.Groups["weights"].Value}[0x2]*{m.Groups["arg1"].Value}['colors'][{m.Groups["idx"].Value}+0x2];}}",
                    1
                );
            ok = true;
            return result;
        }

        private bool TryRestoreCleanOriginalBundle(
            string cacheDataDir,
            string destPath,
            string fileName,
            int currentDiskSize,
            string backupFile
        )
        {
            try
            {
                string originalsCacheDataDir =
                    Path.GetDirectoryName(backupFile) ?? GetOriginalsCacheDataDir();
                return TryRestoreVerifiedBackupAtomic(
                    cacheDataDir,
                    originalsCacheDataDir,
                    destPath,
                    fileName,
                    currentDiskSize,
                    backupFile,
                    out _,
                    backupJs =>
                        !backupJs.Contains(ShotLightBattleMarker, StringComparison.Ordinal)
                        && !backupJs.Contains(
                            LegacyShotLightBattleMarkerV1,
                            StringComparison.Ordinal
                        )
                        && !backupJs.Contains(ShotLightCoreMarker, StringComparison.Ordinal)
                        && !backupJs.Contains(ShotLightCfgStartMarker, StringComparison.Ordinal)
                        && !backupJs.Contains(ShadersStartMarker, StringComparison.Ordinal)
                        && !backupJs.Contains(SkinAnimStartMarker, StringComparison.Ordinal)
                );
            }
            catch
            {
                return false;
            }
        }

        private static bool HasOtherCustomPatchesForShotLight(string jsText)
        {
            return jsText.Contains(ShadersStartMarker, StringComparison.Ordinal)
                || jsText.Contains(SkinAnimStartMarker, StringComparison.Ordinal)
                || jsText.Contains(AnimPaintMarker, StringComparison.Ordinal)
                || jsText.Contains("__tsGarageStage", StringComparison.Ordinal)
                || jsText.Contains(RepairCooldownMarker, StringComparison.Ordinal)
                || jsText.Contains(LegacyRepairCooldownMarkerV2, StringComparison.Ordinal)
                || jsText.Contains(LegacyRepairCooldownMarkerV1, StringComparison.Ordinal)
                || ContainsChatTranslatorMarkers(jsText);
        }

        private static string RemoveMarkerSpan(string text, string startMarker, string endMarker)
        {
            int startIdx = text.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIdx < 0)
            {
                return text;
            }

            int endIdx = text.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
            return endIdx < 0
                ? text
                : string.Concat(text.AsSpan(0, startIdx), text.AsSpan(endIdx + endMarker.Length));
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
