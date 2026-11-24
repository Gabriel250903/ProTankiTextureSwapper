using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService
    {
        private const string ComboManagerStartMarker = "/*__TS_COMBO_MGR_START_V1__*/";
        private const string ComboManagerEndMarker = "/*__TS_COMBO_MGR_END_V1__*/";
        private const string ComboHookNgStart = "/*__TS_CM_NG_S__*/";
        private const string ComboHookNgEnd = "/*__TS_CM_NG_E__*/";
        private const string ComboHookTabsStart = "/*__TS_CM_TABS_S__*/";
        private const string ComboHookTabsEnd = "/*__TS_CM_TABS_E__*/";
        private const string ComboHookOrigMarker = "/*__TS_CM_ORIG__*/";
        private const string ComboHookSelStart = "/*__TS_CM_SEL_S__*/";
        private const string ComboHookSelEnd = "/*__TS_CM_SEL_E__*/";

        [GeneratedRegex(
            @"\['(?<tabFn>\$p\d+)'\]\((?<arg>[A-Za-z0-9_$]+)\)\{this\['(?<catFn>\$p\d+)'\]\(\k<arg>,null\);\}"
        )]
        private static partial Regex ComboTabSelectRegex();

        [GeneratedRegex(
            @"\['tabs'\]\(\)\{let\s+(?<t>[A-Za-z0-9_$]+)=Date\['now'\]\(\),(?<s>[A-Za-z0-9_$]+)=[^;]+;return\s+(?<ret>\(?[A-Za-z0-9_$]+\['filter'\].*?\}\)\)\)?);?\}",
            RegexOptions.Singleline
        )]
        private static partial Regex ComboTabsMethodRegex();

        [GeneratedRegex(
            @"\['tabs'\]\(\)\{let\s+(?<t>[A-Za-z0-9_$]+)=Date\['now'\]\(\),(?<s>[A-Za-z0-9_$]+)=[^;]+;return\s*;\}"
        )]
        private static partial Regex ComboEmptyTabsMethodRegex();

        [GeneratedRegex(
            @"'id':'gifts','(?<labelKey>\$p\d+)':'STRING_GARAGE_CATEGORY_BUTTON_PRESENTS'[^}]*\}\];?(?:[A-Za-z0-9_$]+='turrets';)?function\s+[A-Za-z0-9_$]+\([A-Za-z0-9_$]+\)\{return\s+(?<arr>[A-Za-z0-9_$]+)\['find'\]"
        )]
        private static partial Regex ComboCategoryArrayRegex();

        [GeneratedRegex(
            @"(?:var|let|,)\s*(?<discIcon>[A-Za-z0-9_$]+)='/assets/noti/discounted\.png'"
        )]
        private static partial Regex ComboDiscIconRegex();

        [GeneratedRegex(@"this\['lang'\]\['(?<langFn>\$p\d+)'\]\('ALERT_ANSWER_OK'\)")]
        private static partial Regex ComboLangFnRegex();

        [GeneratedRegex(
            @"\['attention'\]&&[A-Za-z0-9_$]+\['id'\]!==[A-Za-z0-9_$]+\['selected'\]\(\)\?0x2:[A-Za-z0-9_$]+\['(?<discProp>\$p\d+)'\]\?0x3:-0x1"
        )]
        private static partial Regex ComboDiscPropRegex();

        [GeneratedRegex(
            @"static\s+\['ɵcmp'\]=[A-Za-z0-9_$]+\(\{'type':[A-Za-z0-9_$]+,'selectors':\[\['app-garage'\]\]"
        )]
        private static partial Regex ComboGarageCmpRegex();

        private void ApplyGarageFeatureHooks(
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
                Log.Warning(ex, "Garage features hook skipped because ProTanki cache is locked.");
                diagnostics?.Add(
                    new BundlePatchDiagnostic
                    {
                        FileName = "(garage)",
                        BundleKind = "Garage",
                        Feature = "GarageFeatures",
                        Status = "Failed",
                        Reason = ex.Message,
                    }
                );
                return;
            }

            string originalsCacheDataDir = GetOriginalsCacheDataDir();

            int patchedCount = 0;

            foreach (string garagePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(garagePath);
                if (fileName.Contains('.'))
                {
                    continue;
                }

                if (!TryReadZstdBundle(garagePath, out byte[] rawBytes, out string originalJs))
                {
                    continue;
                }

                if (!originalJs.Contains("app-garage-tank-preview", StringComparison.Ordinal))
                {
                    continue;
                }

                string workingJs = StripComboManagerFromJs(originalJs);
                workingJs = StripFavoritePaintsFromJs(workingJs);

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (!ComboTabsMethodRegex().IsMatch(workingJs) && File.Exists(backupFile))
                {
                    if (
                        TryReadZstdBundle(
                            backupFile,
                            out byte[] backupBytes,
                            out string cleanBackupJs
                        )
                        && TryVerifyBackupManifest(
                            originalsCacheDataDir,
                            cacheDataDir,
                            fileName,
                            backupBytes,
                            out _
                        )
                    )
                    {
                        string healedBackupJs = StripFavoritePaintsFromJs(
                            StripComboManagerFromJs(cleanBackupJs)
                        );
                        workingJs = healedBackupJs;
                        if (
                            healedBackupJs != cleanBackupJs
                            && ComboTabsMethodRegex().IsMatch(healedBackupJs)
                        )
                        {
                            byte[] healedBytes = CompressZstdRaw(
                                Encoding.UTF8.GetBytes(healedBackupJs)
                            );
                            SaveBackupAndManifest(
                                originalsCacheDataDir,
                                cacheDataDir,
                                fileName,
                                healedBytes,
                                "GarageFeatures",
                                healedBackupJs,
                                overwriteExistingBackup: true
                            );
                        }
                        Log.Information(
                            "Self-healed Garage bundle {File} from original backup.",
                            fileName
                        );
                    }
                }
                else if (
                    File.Exists(backupFile)
                    && TryReadZstdBundle(
                        backupFile,
                        out byte[] existingBackupBytes,
                        out string existingBackupJs
                    )
                    && TryVerifyBackupManifest(
                        originalsCacheDataDir,
                        cacheDataDir,
                        fileName,
                        existingBackupBytes,
                        out _
                    )
                )
                {
                    string healedBackup = HealEmptyTabsReturnInJs(existingBackupJs);
                    if (
                        healedBackup != existingBackupJs
                        && ComboTabsMethodRegex().IsMatch(healedBackup)
                    )
                    {
                        byte[] healedBytes = CompressZstdRaw(Encoding.UTF8.GetBytes(healedBackup));
                        SaveBackupAndManifest(
                            originalsCacheDataDir,
                            cacheDataDir,
                            fileName,
                            healedBytes,
                            "GarageFeatures",
                            healedBackup,
                            overwriteExistingBackup: true
                        );
                        Log.Information("Healed backup Garage bundle {File}.", fileName);
                    }
                }

                string cleanBeforeHooks = workingJs;
                string fingerprint = ComputeBuildFingerprint(cleanBeforeHooks);

                List<string> comboMissing = [];
                if (
                    !TryPatchGarageForComboManagerHooks(
                        workingJs,
                        out workingJs,
                        out string? comboErr,
                        comboMissing
                    )
                )
                {
                    Log.Warning(
                        "Combo Manager hook skipped on {File}: {Error}",
                        fileName,
                        comboErr
                    );
                    diagnostics?.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Garage",
                            Feature = "ComboManager",
                            Status = "Skipped",
                            BuildFingerprint = fingerprint,
                            Reason = comboErr,
                            MissingSignatures = comboMissing,
                        }
                    );
                }
                else
                {
                    diagnostics?.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Garage",
                            Feature = "ComboManager",
                            Status = "Patched",
                            BuildFingerprint = fingerprint,
                        }
                    );
                }

                List<string> favMissing = [];
                if (
                    !TryPatchGarageForFavoritePaintsHooks(
                        workingJs,
                        out workingJs,
                        out string? favErr,
                        favMissing
                    )
                )
                {
                    Log.Warning(
                        "Favorite Paints hook skipped on {File}: {Error}",
                        fileName,
                        favErr
                    );
                    diagnostics?.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Garage",
                            Feature = "FavoritePaints",
                            Status = "Skipped",
                            BuildFingerprint = fingerprint,
                            Reason = favErr,
                            MissingSignatures = favMissing,
                        }
                    );
                }
                else
                {
                    diagnostics?.Add(
                        new BundlePatchDiagnostic
                        {
                            FileName = fileName,
                            BundleKind = "Garage",
                            Feature = "FavoritePaints",
                            Status = "Patched",
                            BuildFingerprint = fingerprint,
                        }
                    );
                }

                byte[] cleanBackupBytes =
                    cleanBeforeHooks != originalJs
                    && ComboTabsMethodRegex().IsMatch(cleanBeforeHooks)
                        ? CompressZstdRaw(Encoding.UTF8.GetBytes(cleanBeforeHooks))
                        : rawBytes;

                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    garagePath,
                    fileName,
                    cleanBackupBytes,
                    rawBytes.Length,
                    workingJs,
                    saveBackup: !HasOtherCustomPatchesForGarage(originalJs),
                    patchName: "GarageFeatures",
                    cleanOriginalJsForFingerprint: cleanBeforeHooks
                );
                patchedCount++;
                Log.Information("Applied HTML5 Garage feature hooks to {File}.", fileName);
            }

            if (patchedCount > 0)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private void RestoreGarageFeatureHooks(string cacheRootPath)
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
                Log.Warning(ex, "RestoreGarageFeatureHooks aborted due to cache lock.");
                return;
            }

            string originalsCacheDataDir = GetOriginalsCacheDataDir();
            bool anyModified = false;

            foreach (string garagePath in Directory.EnumerateFiles(cacheDataDir, "f_*"))
            {
                string fileName = Path.GetFileName(garagePath);
                if (fileName.Contains('.'))
                {
                    continue;
                }

                if (!TryReadZstdBundle(garagePath, out byte[] rawBytes, out string currentJs))
                {
                    continue;
                }

                bool hasCombo =
                    currentJs.Contains(ComboManagerStartMarker, StringComparison.Ordinal)
                    || currentJs.Contains(ComboHookNgStart, StringComparison.Ordinal)
                    || currentJs.Contains(ComboHookTabsStart, StringComparison.Ordinal)
                    || currentJs.Contains(ComboHookSelStart, StringComparison.Ordinal);
                bool hasFav =
                    currentJs.Contains(FavPaintsStartMarker, StringComparison.Ordinal)
                    || currentJs.Contains(FavPaintsSortHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(FavPaintsRenderHookStart, StringComparison.Ordinal)
                    || currentJs.Contains(FavPaintsGarageHookStart, StringComparison.Ordinal);

                if (!hasCombo && !hasFav)
                {
                    continue;
                }

                string backupFile = Path.Combine(originalsCacheDataDir, fileName);
                if (
                    !HasOtherCustomPatchesForGarage(currentJs)
                    && File.Exists(backupFile)
                    && TryRestoreCleanOriginalBundle(
                        cacheDataDir,
                        garagePath,
                        fileName,
                        rawBytes.Length,
                        backupFile
                    )
                )
                {
                    anyModified = true;
                    Log.Information("Restored clean Garage bundle {File} from backup.", fileName);
                    continue;
                }

                string stripped = StripComboManagerFromJs(currentJs);
                stripped = StripFavoritePaintsFromJs(stripped);
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    garagePath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    stripped,
                    saveBackup: false
                );
                anyModified = true;
                Log.Information("Removed HTML5 Garage feature hooks from {File}.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private static bool HasOtherCustomPatchesForGarage(string jsText)
        {
            return jsText.Contains("__tsGarageStage", StringComparison.Ordinal)
                || jsText.Contains(ShadersStartMarker, StringComparison.Ordinal)
                || jsText.Contains(SkinAnimStartMarker, StringComparison.Ordinal)
                || jsText.Contains(AnimPaintMarker, StringComparison.Ordinal)
                || ContainsChatTranslatorMarkers(jsText);
        }

        private static string StripComboManagerFromJs(string jsText)
        {
            string result = RemoveMarkerSpan(
                jsText,
                ComboManagerStartMarker,
                ComboManagerEndMarker
            );
            result = RemoveMarkerSpan(result, ComboHookNgStart, ComboHookNgEnd);
            result = RemoveMarkerSpan(result, ComboHookSelStart, ComboHookSelEnd);

            int startIdx;
            while ((startIdx = result.IndexOf(ComboHookTabsStart, StringComparison.Ordinal)) >= 0)
            {
                int endIdx = result.IndexOf(ComboHookTabsEnd, startIdx, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    break;
                }

                string span = result[(startIdx + ComboHookTabsStart.Length)..endIdx];
                string originalExpr = string.Empty;
                int origMarkerIdx = span.IndexOf(ComboHookOrigMarker, StringComparison.Ordinal);
                if (origMarkerIdx >= 0 && span.EndsWith(')'))
                {
                    originalExpr = span[(origMarkerIdx + ComboHookOrigMarker.Length)..^1];
                }
                else
                {
                    int sepIdx = span.IndexOf(")):(", StringComparison.Ordinal);
                    if (sepIdx >= 0 && span.EndsWith(')'))
                    {
                        originalExpr = span[(sepIdx + 3)..^1];
                    }
                }
                if (originalExpr.Length >= 2 && originalExpr[0] == '(' && originalExpr[^1] == ')')
                {
                    originalExpr = originalExpr[1..^1];
                }
                result = string.Concat(
                    result.AsSpan(0, startIdx),
                    originalExpr,
                    result.AsSpan(endIdx + ComboHookTabsEnd.Length)
                );
            }

            return HealEmptyTabsReturnInJs(result);
        }

        private static string HealEmptyTabsReturnInJs(string js)
        {
            Match m = ComboEmptyTabsMethodRegex().Match(js);
            if (!m.Success)
            {
                return js;
            }

            string tVar = m.Groups["t"].Value;
            string sVar = m.Groups["s"].Value;

            Match arrMatch = ComboCategoryArrayRegex().Match(js);
            string arrVar = arrMatch.Success ? arrMatch.Groups["arr"].Value : "Xi";
            string labelKey = arrMatch.Success ? arrMatch.Groups["labelKey"].Value : "$p2586";

            Match iconMatch = ComboDiscIconRegex().Match(js);
            string discIcon = iconMatch.Success ? iconMatch.Groups["discIcon"].Value : "R0";

            Match langMatch = ComboLangFnRegex().Match(js);
            string langFn = langMatch.Success ? langMatch.Groups["langFn"].Value : "$p2018";

            Match propMatch = ComboDiscPropRegex().Match(js);
            string discProp = propMatch.Success ? propMatch.Groups["discProp"].Value : "$p2316";

            string retExpr =
                $"{arrVar}['filter'](_0x1=>!this['hidden'](_0x1))['map'](_0x2=>({{'id':_0x2['id'],'label':this['lang']['{langFn}'](_0x2['{labelKey}']),'iconSrc':_0x2['iconSrc'],'attention':{sVar}['has'](_0x2['viewCategory']),'{discProp}':this['discounted'](_0x2,{tVar})?{discIcon}:void 0x0}}))";

            int retKeywordIdx = m.Value.LastIndexOf("return", StringComparison.Ordinal);
            string fixedMethod = string.Concat(
                m.Value.AsSpan(0, retKeywordIdx),
                $"return {retExpr};}}"
            );

            return string.Concat(js.AsSpan(0, m.Index), fixedMethod, js.AsSpan(m.Index + m.Length));
        }

        private static bool TryPatchGarageForComboManagerHooks(
            string cleanJs,
            out string patchedJs,
            out string? error,
            List<string>? missingSignatures = null
        )
        {
            patchedJs = HealEmptyTabsReturnInJs(cleanJs);
            error = null;

            Match cmpMatch = ComboGarageCmpRegex().Match(patchedJs);
            if (!cmpMatch.Success)
            {
                missingSignatures?.Add("ComboGarageCmp");
                error = "Could not locate app-garage component in Garage bundle.";
                return false;
            }

            int ngIdx = patchedJs.LastIndexOf(
                "['ngOnChanges'](",
                cmpMatch.Index,
                StringComparison.Ordinal
            );
            if (ngIdx < 0)
            {
                missingSignatures?.Add("ComboNgOnChanges");
                error = "Could not locate app-garage ngOnChanges hook.";
                return false;
            }

            int ngBraceIdx = patchedJs.IndexOf('{', ngIdx);
            if (ngBraceIdx < 0 || ngBraceIdx > cmpMatch.Index)
            {
                missingSignatures?.Add("ComboNgOnChangesBody");
                error = "Could not locate app-garage ngOnChanges body.";
                return false;
            }

            Match selMatch = ComboTabSelectRegex().Match(patchedJs);
            if (!selMatch.Success)
            {
                missingSignatures?.Add("ComboTabSelect");
                error = "Could not locate app-garage tab selection handler.";
                return false;
            }

            Match tabsMatch = ComboTabsMethodRegex().Match(patchedJs);
            if (!tabsMatch.Success)
            {
                missingSignatures?.Add("ComboTabsMethod");
                error = "Could not locate app-garage tabs() method.";
                return false;
            }

            string tabFn = selMatch.Groups["tabFn"].Value;
            string catFn = selMatch.Groups["catFn"].Value;
            string selArg = selMatch.Groups["arg"].Value;

            string ngHook =
                $"{ComboHookNgStart}window.__tsComboGarage=this;this.__tsTabFn='{tabFn}';this.__tsCatFn='{catFn}';window.__tsComboMgr&&window.__tsComboMgr.onChanges(this);{ComboHookNgEnd}";
            patchedJs = patchedJs.Insert(ngBraceIdx + 1, ngHook);

            selMatch = ComboTabSelectRegex().Match(patchedJs);
            if (!selMatch.Success)
            {
                error = "Failed to re-locate tab selection handler.";
                return false;
            }

            int selBraceIdx = patchedJs.IndexOf('{', selMatch.Index);
            string selHook =
                $"{ComboHookSelStart}this.__tsTabFn='{tabFn}';this.__tsCatFn='{catFn}';if(window.__tsComboMgr&&window.__tsComboMgr.onSelectTab(this,{selArg}))return;{ComboHookSelEnd}";
            patchedJs = patchedJs.Insert(selBraceIdx + 1, selHook);

            tabsMatch = ComboTabsMethodRegex().Match(patchedJs);
            if (!tabsMatch.Success)
            {
                error = "Failed to re-locate tabs() method.";
                return false;
            }

            Group retGroup = tabsMatch.Groups["ret"];
            string wrappedRet =
                $"{ComboHookTabsStart}(window.__tsComboMgr?window.__tsComboMgr.patchTabs(this,({retGroup.Value})):{ComboHookOrigMarker}({retGroup.Value})){ComboHookTabsEnd}";
            patchedJs = string.Concat(
                patchedJs.AsSpan(0, retGroup.Index),
                wrappedRet,
                patchedJs.AsSpan(retGroup.Index + retGroup.Length)
            );

            return true;
        }
    }
}
