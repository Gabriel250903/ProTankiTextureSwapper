using System.Text.RegularExpressions;

namespace TextureSwapper.Services
{
    public partial class Html5CacheService
    {
        private const string FavPaintsStartMarker = "/*__TS_FAV_PAINTS_START_V1__*/";
        private const string FavPaintsEndMarker = "/*__TS_FAV_PAINTS_END_V1__*/";
        private const string FavPaintsSortHookStart = "/*__TS_FP_SORT_S__*/";
        private const string FavPaintsSortHookEnd = "/*__TS_FP_SORT_E__*/";
        private const string FavPaintsOrigMarker = "/*__TS_FP_ORIG__*/";
        private const string FavPaintsRenderHookStart = "/*__TS_FP_RND_S__*/";
        private const string FavPaintsRenderHookEnd = "/*__TS_FP_RND_E__*/";
        private const string FavPaintsGarageHookStart = "/*__TS_FP_GRG_S__*/";
        private const string FavPaintsGarageHookEnd = "/*__TS_FP_GRG_E__*/";

        [GeneratedRegex(
            @"\['(?<sortProp>\$p\d+)'\]=(?<compFn>[A-Za-z0-9_$]+)\(\(\)=>(?<expr>\[\.\.\.this\['items'\]\(\)\]\['sort'\]\((?<cmp>[A-Za-z0-9_$]+)\))\);"
        )]
        private static partial Regex FavPaintsSortRegex();

        [GeneratedRegex(
            @"\['\$p\d+'\]\(\)\{let\s+[A-Za-z0-9_$]+=this\['pane'\]\(\)\?\.\['viewportElement'\],(?<el>[A-Za-z0-9_$]+)=this\['\$p\d+'\]\(\)\?\.\['nativeElement'\];if\([^\)]+\)return;"
        )]
        private static partial Regex FavPaintsRenderRegex();

        [GeneratedRegex(
            @"\['(?<refreshFn>\$p\d+)'\]\(\)\{if\(this\['hidden'\]\([A-Za-z0-9_$]+\(this\['category'\]\)\)\)\{"
        )]
        private static partial Regex FavPaintsGarageRefreshRegex();

        private static string StripFavoritePaintsFromJs(string jsText)
        {
            string result = RemoveMarkerSpan(jsText, FavPaintsStartMarker, FavPaintsEndMarker);
            result = RemoveMarkerSpan(result, FavPaintsRenderHookStart, FavPaintsRenderHookEnd);
            result = RemoveMarkerSpan(result, FavPaintsGarageHookStart, FavPaintsGarageHookEnd);

            int startIdx;
            while (
                (startIdx = result.IndexOf(FavPaintsSortHookStart, StringComparison.Ordinal)) >= 0
            )
            {
                int endIdx = result.IndexOf(
                    FavPaintsSortHookEnd,
                    startIdx,
                    StringComparison.Ordinal
                );
                if (endIdx < 0)
                {
                    break;
                }

                string span = result[(startIdx + FavPaintsSortHookStart.Length)..endIdx];
                string originalExpr = string.Empty;
                int origMarkerIdx = span.IndexOf(FavPaintsOrigMarker, StringComparison.Ordinal);
                if (origMarkerIdx >= 0 && span.EndsWith(')'))
                {
                    originalExpr = span[(origMarkerIdx + FavPaintsOrigMarker.Length)..^1];
                }
                else
                {
                    int sepIdx = span.IndexOf("):", StringComparison.Ordinal);
                    if (sepIdx >= 0 && span.EndsWith(')'))
                    {
                        originalExpr = span[(sepIdx + 2)..^1];
                    }
                }
                result = string.Concat(
                    result.AsSpan(0, startIdx),
                    originalExpr,
                    result.AsSpan(endIdx + FavPaintsSortHookEnd.Length)
                );
            }

            return result;
        }

        private static bool TryPatchGarageForFavoritePaintsHooks(
            string cleanJs,
            out string patchedJs,
            out string? error,
            List<string>? missingSignatures = null
        )
        {
            patchedJs = cleanJs;
            error = null;

            Match sortMatch = FavPaintsSortRegex().Match(patchedJs);
            if (!sortMatch.Success)
            {
                missingSignatures?.Add("FavPaintsSort");
                error = "Could not locate app-garage-items-list sort expression in Garage bundle.";
                return false;
            }

            Group exprGroup = sortMatch.Groups["expr"];
            string cmpVar = sortMatch.Groups["cmp"].Value;
            string wrappedSort =
                $"{FavPaintsSortHookStart}(window.__tsFavPaints?window.__tsFavPaints.sort([...this['items']()],{cmpVar}):{FavPaintsOrigMarker}{exprGroup.Value}){FavPaintsSortHookEnd}";
            patchedJs = string.Concat(
                patchedJs.AsSpan(0, exprGroup.Index),
                wrappedSort,
                patchedJs.AsSpan(exprGroup.Index + exprGroup.Length)
            );

            Match rndMatch = FavPaintsRenderRegex().Match(patchedJs);
            if (!rndMatch.Success)
            {
                missingSignatures?.Add("FavPaintsRender");
                error = "Could not locate app-garage-items-list card sync handler.";
                return false;
            }

            string elVar = rndMatch.Groups["el"].Value;
            int rndInsertIdx = rndMatch.Index + rndMatch.Length;
            string rndHook =
                $"{FavPaintsRenderHookStart}window.__tsFavPaints&&window.__tsFavPaints.syncCards({elVar},this['items']());{FavPaintsRenderHookEnd}";
            patchedJs = patchedJs.Insert(rndInsertIdx, rndHook);

            Match grgMatch = FavPaintsGarageRefreshRegex().Match(patchedJs);
            if (!grgMatch.Success)
            {
                missingSignatures?.Add("FavPaintsGarageRefresh");
                error = "Could not locate app-garage refresh method.";
                return false;
            }

            string refreshFn = grgMatch.Groups["refreshFn"].Value;
            int grgBraceIdx = patchedJs.IndexOf('{', grgMatch.Index);
            string grgHook =
                $"{FavPaintsGarageHookStart}window.__tsFavGarage=this;this.__tsFavRefreshFn='{refreshFn}';{FavPaintsGarageHookEnd}";
            patchedJs = patchedJs.Insert(grgBraceIdx + 1, grgHook);

            return true;
        }
    }
}
