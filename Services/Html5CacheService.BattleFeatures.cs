using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService
    {
        private const string BfSpawnHookStart = "/*__TS_BF_SPN_S__*/";
        private const string BfSpawnHookEnd = "/*__TS_BF_SPN_E__*/";
        private const string BfListHookStart = "/*__TS_BF_LST_S__*/";
        private const string BfListHookEnd = "/*__TS_BF_LST_E__*/";
        private const string BfTakenHookStart = "/*__TS_BF_TKN_S__*/";
        private const string BfTakenHookEnd = "/*__TS_BF_TKN_E__*/";
        private const string BfRemHookStart = "/*__TS_BF_REM_S__*/";
        private const string BfRemHookEnd = "/*__TS_BF_REM_E__*/";
        private const string BfEndHookStart = "/*__TS_BF_END_S__*/";
        private const string BfEndHookEnd = "/*__TS_BF_END_E__*/";
        private const string BfInitHookStart = "/*__TS_BF_INI_S__*/";
        private const string BfInitHookEnd = "/*__TS_BF_INI_E__*/";
        private const string BfFinHookStart = "/*__TS_BF_FIN_S__*/";
        private const string BfFinHookEnd = "/*__TS_BF_FIN_E__*/";
        private const string BtlCamHookStart = "/*__TS_BTL_CAM_S__*/";
        private const string BtlCamHookEnd = "/*__TS_BTL_CAM_E__*/";
        private const string BtlRecHookStart = "/*__TS_BTL_REC_S__*/";
        private const string BtlRecHookEnd = "/*__TS_BTL_REC_E__*/";
        private const string BtlPickHookStart = "/*__TS_BTL_PCK_S__*/";
        private const string BtlPickHookEnd = "/*__TS_BTL_PCK_E__*/";
        private const string BtlRemHookStart = "/*__TS_BTL_REM_S__*/";
        private const string BtlRemHookEnd = "/*__TS_BTL_REM_E__*/";

        [GeneratedRegex(
            @"\['\$p2302'\]\((?<a>[A-Za-z0-9_$]+)\)\{this\['bonuses'\]\['\$p2302'\]\(\k<a>\);"
        )]
        private static partial Regex BfBonusListRegex();

        [GeneratedRegex(
            @"\['\$p5033'\]\((?<a>[A-Za-z0-9_$]+),(?<b>[A-Za-z0-9_$]+),(?<c>[A-Za-z0-9_$]+)\)\{this\['bonuses'\]\['\$p5033'\]\(\k<a>,\k<b>,\k<c>\);"
        )]
        private static partial Regex BfBonusSpawnRegex();

        [GeneratedRegex(
            @"\['\$p4128'\]\((?<a>[A-Za-z0-9_$]+)\)\{this\['bonuses'\]\['\$p4128'\]\(\k<a>\);"
        )]
        private static partial Regex BfBonusTakenRegex();

        [GeneratedRegex(
            @"\['\$p615'\]\((?<a>[A-Za-z0-9_$]+)\)\{this\['bonuses'\]\['\$p615'\]\(\k<a>\);"
        )]
        private static partial Regex BfBonusRemoveRegex();

        [GeneratedRegex(@"\['\$p5654'\]\(\)\{this\['tanks'\]\['\$p5656'\]\(\),")]
        private static partial Regex BfBattleUnloadRegex();

        [GeneratedRegex(
            @"\['\$p2317'\]\((?<a>[A-Za-z0-9_$]+),(?<b>[A-Za-z0-9_$]+),(?<c>[A-Za-z0-9_$]+),(?<d>[A-Za-z0-9_$]+),(?<e>[A-Za-z0-9_$]+)\)\{this\['battlefield'\]\['initialized'\]\("
        )]
        private static partial Regex BfBattleInitRegex();

        [GeneratedRegex(
            @"\['\$p505'\]\(\)\{this\['\$p1291'\]\['log'\]\('\[battlefield\]\\x20Battle\\x20finished\.'\),"
        )]
        private static partial Regex BfBattleFinishRegex();

        [GeneratedRegex(
            @"let\s+(?<cam>[A-Za-z0-9_$]+)=this\['config'\]\['camera'\];\k<cam>\['updateMatrixWorld'\]\(\),this\['(?<vp>\$p\d+)'\]\['multiplyMatrices'\]\(\k<cam>\['projectionMatrix'\],\k<cam>\['matrixWorldInverse'\]\)"
        )]
        private static partial Regex BtlCamVpRegex();

        [GeneratedRegex(
            @"\['reconcile'\]\((?<a>[A-Za-z0-9_$]+)\)\{this\['\$p\d+'\]\['clear'\]\(\);for\(let\s+[A-Za-z0-9_$]+\s+of\s+\k<a>\)this\['\$p\d+'\]\['set'\]\([A-Za-z0-9_$]+\['bonusId'\]"
        )]
        private static partial Regex BtlHuReconcileRegex();

        [GeneratedRegex(
            @"\['\$p\d+'\]\((?<a>[A-Za-z0-9_$]+)\)\{for\(let\s+(?<e>[A-Za-z0-9_$]+)\s+of\s+\k<a>\)\{if\(this\['\$p\d+'\]\['has'\]\(\k<e>\['\$p\d+'\]\)\)continue;this\['\$p\d+'\]\['add'\]\(\k<e>\['\$p\d+'\]\);let\s+[A-Za-z0-9_$]+=this\['\$p\d+'\]\['get'\]\(\k<e>\['bonusId'\]\)"
        )]
        private static partial Regex BtlHuPickupRegex();

        [GeneratedRegex(
            @"\['\$p\d+'\]\((?<a>[A-Za-z0-9_$]+)\)\{for\(let\s+(?<e>[A-Za-z0-9_$]+)\s+of\s+\k<a>\)this\['\$p\d+'\]\['has'\]\(\k<e>\['\$p\d+'\]\)\|\|\(this\['\$p\d+'\]\['add'\]\(\k<e>\['\$p\d+'\]\),this\['\$p\d+'\]\['get'\]\(\k<e>\['bonusId'\]\)"
        )]
        private static partial Regex BtlHuRemoveRegex();

        private void ApplyBattleFeatureHooks(
            string cacheRootPath,
            List<BundlePatchDiagnostic>? diagnostics = null
        )
        {
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
                Log.Warning(ex, "Battle features hook skipped because ProTanki cache is locked.");
                diagnostics?.Add(
                    new BundlePatchDiagnostic
                    {
                        FileName = "(battle)",
                        BundleKind = "Battle",
                        Feature = "BattleFeatures",
                        Status = "Failed",
                        Reason = ex.Message,
                    }
                );
                return;
            }

            string originalsCacheDataDir = GetOriginalsCacheDataDir();

            bool allowSupplyTimers = ChatService.IsAdminDevice;
            int patchedCount = 0;

            foreach (string bundlePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(bundlePath);
                if (fileName.Contains('.'))
                {
                    continue;
                }

                if (!TryReadZstdBundle(bundlePath, out byte[] rawBytes, out string originalJs))
                {
                    continue;
                }

                bool isCore = originalJs.Contains(
                    "[battlefield]\\x20Battle\\x20finished.",
                    StringComparison.Ordinal
                );
                bool isBattle =
                    originalJs.Contains("pickupSoundResource", StringComparison.Ordinal)
                    && originalJs.Contains("multiplyMatrices", StringComparison.Ordinal);
                if (!isCore && !isBattle)
                {
                    continue;
                }

                bool hasExistingBtlHooks =
                    isBattle
                    && (
                        originalJs.Contains(BtlCamHookStart, StringComparison.Ordinal)
                        || originalJs.Contains(BtlRecHookStart, StringComparison.Ordinal)
                        || originalJs.Contains(BtlPickHookStart, StringComparison.Ordinal)
                        || originalJs.Contains(BtlRemHookStart, StringComparison.Ordinal)
                    );

                if (!isCore && !allowSupplyTimers && !hasExistingBtlHooks)
                {
                    continue;
                }

                string workingJs = originalJs;
                string cleanBeforeHooks = workingJs;
                if (isCore)
                {
                    workingJs = StripBattleFeaturesFromJs(workingJs);
                    cleanBeforeHooks = workingJs;
                    List<string> missingCore = [];
                    if (
                        !TryPatchBundleForBattleFeatures(
                            workingJs,
                            allowSupplyTimers,
                            out workingJs,
                            out string? err,
                            missingCore
                        )
                    )
                    {
                        Log.Warning(
                            "Battle features hook skipped on {File}: {Error}",
                            fileName,
                            err
                        );
                        diagnostics?.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "CoreBattle",
                                Feature = "BattleFeatures",
                                Status = "Skipped",
                                BuildFingerprint = ComputeBuildFingerprint(cleanBeforeHooks),
                                Reason = err,
                                MissingSignatures = missingCore,
                            }
                        );
                        continue;
                    }
                    diagnostics?.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "CoreBattle",
                            Feature = "BattleFeatures",
                            Status = "Patched",
                            BuildFingerprint = ComputeBuildFingerprint(cleanBeforeHooks),
                        }
                    );
                }
                if (isBattle)
                {
                    workingJs = StripLiveBattleRenderHooksFromJs(workingJs);
                    cleanBeforeHooks = workingJs;
                    List<string> missingBattle = [];
                    if (
                        allowSupplyTimers
                        && !TryPatchBattleBundleForTimers(
                            workingJs,
                            out workingJs,
                            out string? bErr,
                            missingBattle
                        )
                    )
                    {
                        Log.Warning("Battle timer hook skipped on {File}: {Error}", fileName, bErr);
                        diagnostics?.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "BattleRender",
                                Feature = "SupplyTimers",
                                Status = "Skipped",
                                BuildFingerprint = ComputeBuildFingerprint(cleanBeforeHooks),
                                Reason = bErr,
                                MissingSignatures = missingBattle,
                            }
                        );
                        continue;
                    }
                    if (allowSupplyTimers)
                    {
                        diagnostics?.Add(
                            new BundlePatchDiagnostic
                            {
                                FileName = fileName,
                                BundleKind = "BattleRender",
                                Feature = "SupplyTimers",
                                Status = "Patched",
                                BuildFingerprint = ComputeBuildFingerprint(cleanBeforeHooks),
                            }
                        );
                    }
                }

                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    bundlePath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    workingJs,
                    saveBackup: !HasOtherCustomPatchesForGarage(originalJs),
                    patchName: "BattleFeatures",
                    cleanOriginalJsForFingerprint: cleanBeforeHooks
                );
                patchedCount++;
                Log.Information("Applied HTML5 Battle feature hooks to {File}.", fileName);
            }

            if (patchedCount > 0)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private void RestoreBattleFeatureHooks(string cacheRootPath)
        {
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
                Log.Warning(ex, "RestoreBattleFeatureHooks aborted due to cache lock.");
                return;
            }

            string originalsCacheDataDir = GetOriginalsCacheDataDir();
            bool anyModified = false;

            foreach (string bundlePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(bundlePath);
                if (fileName.Contains('.'))
                {
                    continue;
                }

                if (!TryReadZstdBundle(bundlePath, out byte[] rawBytes, out string currentJs))
                {
                    continue;
                }

                bool hasBf =
                    currentJs.Contains(BfSpawnHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfListHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfTakenHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfRemHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfEndHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfInitHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BfFinHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BtlCamHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BtlRecHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BtlPickHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(BtlRemHookStart, StringComparison.Ordinal);

                if (!hasBf)
                {
                    continue;
                }

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (
                    !HasOtherCustomPatchesForGarage(currentJs)
                    && File.Exists(backupFile)
                    && TryRestoreCleanOriginalBundle(
                        cacheDataDir,
                        bundlePath,
                        fileName,
                        rawBytes.Length,
                        backupFile
                    )
                )
                {
                    anyModified = true;
                    Log.Information("Restored clean core bundle {File} from backup.", fileName);
                    continue;
                }

                string stripped = StripBattleFeaturesFromJs(currentJs);
                stripped = StripLiveBattleRenderHooksFromJs(stripped);
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    bundlePath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    stripped,
                    saveBackup: false
                );
                anyModified = true;
                Log.Information("Removed HTML5 Battle feature hooks from {File}.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private static string StripBattleFeaturesFromJs(string jsText)
        {
            string r = RemoveMarkerSpan(jsText, BfSpawnHookStart, BfSpawnHookEnd);
            r = RemoveMarkerSpan(r, BfListHookStart, BfListHookEnd);
            r = RemoveMarkerSpan(r, BfTakenHookStart, BfTakenHookEnd);
            r = RemoveMarkerSpan(r, BfRemHookStart, BfRemHookEnd);
            r = RemoveMarkerSpan(r, BfEndHookStart, BfEndHookEnd);
            r = RemoveMarkerSpan(r, BfInitHookStart, BfInitHookEnd);
            r = RemoveMarkerSpan(r, BfFinHookStart, BfFinHookEnd);
            return r;
        }

        private static bool TryPatchBundleForBattleFeatures(
            string cleanJs,
            bool includeSupplyTimers,
            out string patchedJs,
            out string? error,
            List<string>? missingSignatures = null
        )
        {
            patchedJs = cleanJs;
            error = null;

            if (includeSupplyTimers)
            {
                Match lst = BfBonusListRegex().Match(patchedJs);
                if (lst.Success)
                {
                    patchedJs = patchedJs.Insert(
                        lst.Index + lst.Length,
                        $"{BfListHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onBonusList({lst.Groups["a"].Value});{BfListHookEnd}"
                    );
                }
                else
                {
                    missingSignatures?.Add("BfBonusList");
                }

                Match spn = BfBonusSpawnRegex().Match(patchedJs);
                if (!spn.Success)
                {
                    missingSignatures?.Add("BfBonusSpawn");
                    error = "Could not locate bonus spawn handler.";
                    return false;
                }
                patchedJs = patchedJs.Insert(
                    spn.Index + spn.Length,
                    $"{BfSpawnHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onBonusSpawn({spn.Groups["a"].Value},{spn.Groups["b"].Value},{spn.Groups["c"].Value});{BfSpawnHookEnd}"
                );

                Match tkn = BfBonusTakenRegex().Match(patchedJs);
                if (tkn.Success)
                {
                    patchedJs = patchedJs.Insert(
                        tkn.Index + tkn.Length,
                        $"{BfTakenHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onBonusPickup({tkn.Groups["a"].Value});{BfTakenHookEnd}"
                    );
                }

                Match rem = BfBonusRemoveRegex().Match(patchedJs);
                if (rem.Success)
                {
                    patchedJs = patchedJs.Insert(
                        rem.Index + rem.Length,
                        $"{BfRemHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onBonusPickup({rem.Groups["a"].Value});{BfRemHookEnd}"
                    );
                }

                Match unl = BfBattleUnloadRegex().Match(patchedJs);
                if (unl.Success)
                {
                    patchedJs = patchedJs.Insert(
                        unl.Index + unl.Length,
                        $"{BfEndHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onBattleEnd();{BfEndHookEnd}"
                    );
                }

                Match ini = BfBattleInitRegex().Match(patchedJs);
                if (ini.Success)
                {
                    int braceIdx = patchedJs.IndexOf('{', ini.Index);
                    if (braceIdx >= 0)
                    {
                        patchedJs = patchedJs.Insert(
                            braceIdx + 1,
                            $"{BfInitHookStart}window.__tsBattleHistory&&window.__tsBattleHistory.onBattleInit({ini.Groups["d"].Value});{BfInitHookEnd}"
                        );
                    }
                }

                Match fin = BfBattleFinishRegex().Match(patchedJs);
                if (fin.Success)
                {
                    patchedJs = patchedJs.Insert(
                        fin.Index + fin.Length,
                        $"{BfFinHookStart}window.__tsBattleHistory&&window.__tsBattleHistory.onBattleFinish(this,null);{BfFinHookEnd}"
                    );
                }
            }

            return true;
        }

        private static string StripLiveBattleRenderHooksFromJs(string jsText)
        {
            string r = RemoveMarkerSpan(jsText, BtlCamHookStart, BtlCamHookEnd);
            r = Regex.Replace(r, @"\['matrixWorldInverse'\]\)\s*,+", "['matrixWorldInverse']),");
            r = RemoveMarkerSpan(r, BtlRecHookStart, BtlRecHookEnd);
            r = RemoveMarkerSpan(r, BtlPickHookStart, BtlPickHookEnd);
            r = RemoveMarkerSpan(r, BtlRemHookStart, BtlRemHookEnd);
            return r;
        }

        private static bool TryPatchBattleBundleForTimers(
            string cleanJs,
            out string patchedJs,
            out string? error,
            List<string>? missingSignatures = null
        )
        {
            patchedJs = cleanJs;
            error = null;

            Match cam = BtlCamVpRegex().Match(patchedJs);
            if (!cam.Success)
            {
                missingSignatures?.Add("BtlCamVp");
                error = "Could not locate battle camera VP projector.";
                return false;
            }
            string vpProp = cam.Groups["vp"].Value;
            int insertPos = cam.Index + cam.Length;
            while (
                insertPos + 1 < patchedJs.Length
                && patchedJs[insertPos] == ','
                && patchedJs[insertPos + 1] == ','
            )
            {
                patchedJs = patchedJs.Remove(insertPos, 1);
            }
            patchedJs = patchedJs.Insert(
                insertPos,
                $"{BtlCamHookStart},(window.__ptCamVP=this['{vpProp}']['elements']){BtlCamHookEnd}"
            );

            Match rec = BtlHuReconcileRegex().Match(patchedJs);
            if (rec.Success)
            {
                int braceIdx = patchedJs.IndexOf('{', rec.Index);
                if (braceIdx >= 0)
                {
                    patchedJs = patchedJs.Insert(
                        braceIdx + 1,
                        $"{BtlRecHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onHuReconcile({rec.Groups["a"].Value});{BtlRecHookEnd}"
                    );
                }
            }

            Match pck = BtlHuPickupRegex().Match(patchedJs);
            if (pck.Success)
            {
                int braceIdx = patchedJs.IndexOf('{', pck.Index);
                if (braceIdx >= 0)
                {
                    patchedJs = patchedJs.Insert(
                        braceIdx + 1,
                        $"{BtlPickHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onHuPickup({pck.Groups["a"].Value});{BtlPickHookEnd}"
                    );
                }
            }

            Match rem = BtlHuRemoveRegex().Match(patchedJs);
            if (rem.Success)
            {
                int braceIdx = patchedJs.IndexOf('{', rem.Index);
                if (braceIdx >= 0)
                {
                    patchedJs = patchedJs.Insert(
                        braceIdx + 1,
                        $"{BtlRemHookStart}window.__tsSupplyTimers&&window.__tsSupplyTimers.onHuRemove({rem.Groups["a"].Value});{BtlRemHookEnd}"
                    );
                }
            }

            return true;
        }
    }
}
