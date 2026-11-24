using System.IO;
using System.Text.RegularExpressions;
using Serilog;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService
    {
        private const string CtBattleLinesStart = "/*__TS_CT_BL_S__*/";
        private const string CtBattleLinesEnd = "/*__TS_CT_BL_E__*/";
        private const string CtBattleLinesOrig = "/*__TS_CT_ORIG__*/";
        private const string CtOrigEnd = "/*__TS_CT_ORIG_E__*/";
        private const string CtBattleOutStart = "/*__TS_CT_BO_S__*/";
        private const string CtBattleOutEnd = "/*__TS_CT_BO_E__*/";
        private const string CtLobbyLinesStart = "/*__TS_CT_LL_S__*/";
        private const string CtLobbyLinesEnd = "/*__TS_CT_LL_E__*/";
        private const string CtLobbyOutStart = "/*__TS_CT_LO_S__*/";
        private const string CtLobbyOutEnd = "/*__TS_CT_LO_E__*/";

        [GeneratedRegex(
            @"\['\$p3174'\]=(?<sig>[A-Za-z0-9_$]+)\(\(\)=>(?<expr>\[\.\.\.this\['\$p\d+'\]\(\)\]\['reverse'\]\(\))\)"
        )]
        private static partial Regex CtBattleLinesRegex();

        [GeneratedRegex(
            @"(?<msg>[A-Za-z0-9_$]+)!==''&&\((?<call>this\['chat'\]\['\$p4514'\]\(\k<msg>\)),"
        )]
        private static partial Regex CtBattleOutRegex();

        [GeneratedRegex(
            @"(?<arr>[A-Za-z0-9_$]+)=this\['lines'\]\(\),(?<out>[A-Za-z0-9_$]+)=\k<arr>\['map'\]"
        )]
        private static partial Regex CtLobbyLinesRegex();

        [GeneratedRegex(
            @"let (?<res>[A-Za-z0-9_$]+)=this\['chat'\]\['\$p4522'\]\((?<msg>[A-Za-z0-9_$]+),(?<addr>[A-Za-z0-9_$]+)\);"
        )]
        private static partial Regex CtLobbyOutRegex();

        private void ApplyChatTranslatorHooks(
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
                Log.Warning(ex, "ChatTranslator hook skipped because ProTanki cache is locked.");
                return;
            }

            string originalsCacheDataDir = GetOriginalsCacheDataDir();
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

                bool isBattle = originalJs.Contains("app-battle-chat", StringComparison.Ordinal);
                bool isLobby =
                    originalJs.Contains("app-chat-output", StringComparison.Ordinal)
                    && originalJs.Contains("app-chat-view", StringComparison.Ordinal);

                if (!isBattle && !isLobby)
                {
                    continue;
                }

                string workingJs = StripChatTranslatorFromJs(originalJs);
                string cleanBeforeHooks = workingJs;
                bool modified = false;

                if (isBattle)
                {
                    Match bl = CtBattleLinesRegex().Match(workingJs);
                    if (bl.Success)
                    {
                        string sig = bl.Groups["sig"].Value;
                        string expr = bl.Groups["expr"].Value;
                        string wrapped =
                            $"['$p3174']={sig}(()=>({CtBattleLinesStart}((__tsL)=>(window.__tsChatTranslator&&window.__tsChatTranslator.onBattleLines(__tsL),__tsL))({CtBattleLinesOrig}{expr}{CtOrigEnd}){CtBattleLinesEnd}))";
                        workingJs = string.Concat(
                            workingJs.AsSpan(0, bl.Index),
                            wrapped,
                            workingJs.AsSpan(bl.Index + bl.Length)
                        );
                        modified = true;
                    }

                    Match bo = CtBattleOutRegex().Match(workingJs);
                    if (bo.Success)
                    {
                        string msg = bo.Groups["msg"].Value;
                        string call = bo.Groups["call"].Value;
                        string replacement =
                            $"{msg}!==''&&({CtBattleOutStart}(window.__tsChatTranslator&&window.__tsChatTranslator.handleOutgoing({msg},__tsM=>this['chat']['$p4514'](__tsM)))?void 0x0:{CtBattleOutEnd}{call},";
                        workingJs = string.Concat(
                            workingJs.AsSpan(0, bo.Index),
                            replacement,
                            workingJs.AsSpan(bo.Index + bo.Length)
                        );
                        modified = true;
                    }
                }

                if (isLobby)
                {
                    Match ll = CtLobbyLinesRegex().Match(workingJs);
                    if (ll.Success)
                    {
                        string arr = ll.Groups["arr"].Value;
                        string outVar = ll.Groups["out"].Value;
                        string replacement =
                            $"{arr}={CtLobbyLinesStart}((__tsL)=>(window.__tsChatTranslator&&window.__tsChatTranslator.onLobbyLines(__tsL),__tsL))({CtBattleLinesOrig}this['lines'](){CtOrigEnd}){CtLobbyLinesEnd},{outVar}={arr}['map']";
                        workingJs = string.Concat(
                            workingJs.AsSpan(0, ll.Index),
                            replacement,
                            workingJs.AsSpan(ll.Index + ll.Length)
                        );
                        modified = true;
                    }

                    Match lo = CtLobbyOutRegex().Match(workingJs);
                    if (lo.Success)
                    {
                        string msg = lo.Groups["msg"].Value;
                        string addr = lo.Groups["addr"].Value;
                        string hook =
                            $"{CtLobbyOutStart}if(window.__tsChatTranslator&&window.__tsChatTranslator.handleOutgoing({msg},__tsM=>this['chat']['$p4522'](__tsM,{addr}))){{this['$p853']['set']({addr}?{addr}+':\\x20':'');return;}}{CtLobbyOutEnd}";
                        workingJs = workingJs.Insert(lo.Index, hook);
                        modified = true;
                    }
                }

                if (!modified)
                {
                    continue;
                }

                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    bundlePath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    workingJs,
                    saveBackup: isBattle
                        ? !HasOtherCustomPatchesForShotLight(originalJs)
                        : !HasOtherCustomPatchesForGarage(originalJs),
                    patchName: "ChatTranslator",
                    cleanOriginalJsForFingerprint: cleanBeforeHooks
                );
                diagnostics?.Add(
                    new BundlePatchDiagnostic
                    {
                        FileName = fileName,
                        BundleKind = isBattle ? "BattleChat" : "LobbyChat",
                        Feature = "ChatTranslator",
                        Status = "Patched",
                        BuildFingerprint = ComputeBuildFingerprint(cleanBeforeHooks),
                    }
                );
                patchedCount++;
                Log.Information("Applied HTML5 ChatTranslator hooks to {File}.", fileName);
            }

            if (patchedCount > 0)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private void RestoreChatTranslatorHooks(string cacheRootPath)
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
                Log.Warning(ex, "RestoreChatTranslatorHooks aborted due to cache lock.");
                return;
            }

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

                bool hasLegacyParen =
                    currentJs.Contains("=(this['lines']())),", StringComparison.Ordinal)
                    || currentJs.Contains("['reverse']())))", StringComparison.Ordinal);
                if (!ContainsChatTranslatorMarkers(currentJs) && !hasLegacyParen)
                {
                    continue;
                }

                string restoredJs = StripChatTranslatorFromJs(currentJs);
                if (string.Equals(restoredJs, currentJs, StringComparison.Ordinal))
                {
                    continue;
                }

                string originalsCacheDataDir = GetOriginalsCacheDataDir();
                WritePatchedZstdBundle(
                    cacheDataDir,
                    originalsCacheDataDir,
                    bundlePath,
                    fileName,
                    rawBytes,
                    rawBytes.Length,
                    restoredJs,
                    saveBackup: false
                );

                anyModified = true;
                Log.Information("Removed HTML5 ChatTranslator hooks from {File}.", fileName);
            }

            if (anyModified)
            {
                ClearCodeCacheJs(rootDir);
                InvalidateCache();
            }
        }

        private static bool ContainsChatTranslatorMarkers(string jsText)
        {
            return jsText.Contains(CtBattleLinesStart, StringComparison.Ordinal)
                || jsText.Contains(CtBattleOutStart, StringComparison.Ordinal)
                || jsText.Contains(CtLobbyLinesStart, StringComparison.Ordinal)
                || jsText.Contains(CtLobbyOutStart, StringComparison.Ordinal);
        }

        private static string StripChatTranslatorFromJs(string jsText)
        {
            string result = UnwrapOriginSpan(
                jsText,
                CtBattleLinesStart,
                CtBattleLinesEnd,
                CtBattleLinesOrig
            );
            result = UnwrapOriginSpan(
                result,
                CtLobbyLinesStart,
                CtLobbyLinesEnd,
                CtBattleLinesOrig
            );
            result = RemoveMarkerSpan(result, CtBattleOutStart, CtBattleOutEnd);
            result = RemoveMarkerSpan(result, CtLobbyOutStart, CtLobbyOutEnd);

            result = Regex.Replace(
                result,
                @"(?<arr>[A-Za-z0-9_$]+)=\(this\['lines'\]\(\)\)\),",
                "${arr}=this['lines'](),"
            );
            result = Regex.Replace(
                result,
                @"\['\$p3174'\]=(?<sig>[A-Za-z0-9_$]+)\(\(\)=>\((?<expr>\[\.\.\.this\['\$p\d+'\]\(\)\]\['reverse'\]\(\))\)\)\)",
                "['$p3174']=${sig}(()=>${expr})"
            );
            result = Regex.Replace(
                result,
                @"\['\$p3174'\]=(?<sig>[A-Za-z0-9_$]+)\(\(\)=>\((?<expr>\[\.\.\.this\['\$p\d+'\]\(\)\]\['reverse'\]\(\))\)\)",
                "['$p3174']=${sig}(()=>${expr})"
            );
            return result;
        }

        private static string UnwrapOriginSpan(
            string jsText,
            string startMarker,
            string endMarker,
            string origMarker
        )
        {
            string result = jsText;
            int startIdx;
            while ((startIdx = result.IndexOf(startMarker, StringComparison.Ordinal)) >= 0)
            {
                int endIdx = result.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    break;
                }

                string wrapped = result[
                    (startIdx + startMarker.Length)..endIdx];
                int origIdx = wrapped.IndexOf(origMarker, StringComparison.Ordinal);
                string origExpr = string.Empty;
                if (origIdx >= 0)
                {
                    int afterOrig = origIdx + origMarker.Length;
                    int origEndIdx = wrapped.IndexOf(
                        CtOrigEnd,
                        afterOrig,
                        StringComparison.Ordinal
                    );
                    if (origEndIdx >= 0)
                    {
                        origExpr = wrapped[afterOrig..origEndIdx];
                    }
                    else
                    {
                        origExpr = wrapped[afterOrig..];
                        if (origExpr.StartsWith('(') && origExpr.EndsWith("))"))
                        {
                            origExpr = origExpr[1..^2];
                        }
                        else if (origExpr.StartsWith('(') && origExpr.EndsWith(')'))
                        {
                            origExpr = origExpr[1..^1];
                        }
                        else if (origExpr.EndsWith(')'))
                        {
                            origExpr = origExpr[..^1];
                        }
                    }
                }

                result = string.Concat(
                    result.AsSpan(0, startIdx),
                    origExpr,
                    result.AsSpan(endIdx + endMarker.Length)
                );
            }

            return result;
        }
    }
}
