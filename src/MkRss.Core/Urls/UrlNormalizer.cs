using System;
using System.Collections.Frozen;
using System.Linq;

namespace MkRss.Core.Urls;

/// <summary>
/// 記事URLを同一性判定に使える形へ正規化する。
/// </summary>
public static class UrlNormalizer {
    /// <summary>
    /// 名前がこの接頭辞で始まるクエリパラメータは計測用とみなして除去する。
    /// </summary>
    private const string TrackingParameterPrefix = "utm_";

    /// <summary>
    /// 接頭辞では判別できない計測用クエリパラメータの名前。
    /// </summary>
    private static readonly FrozenSet<string> TrackingParameterNames = new[]
    {
        "fbclid",
        "gclid",
        "dclid",
        "msclkid",
        "yclid",
        "igshid",
        "twclid",
        "mc_cid",
        "mc_eid",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="href"/> を <paramref name="baseUri"/> を基準に正規化する。
    /// 記事のURLとして使えない場合は <see langword="null"/> を返す。
    /// </summary>
    public static string? Normalize(Uri baseUri, string href) {
        if (string.IsNullOrWhiteSpace(href)) {
            return null;
        }

        if (!Uri.TryCreate(baseUri, href, out Uri? absolute)) {
            return null;
        }

        // javascript: や mailto: など、記事を指し得ないスキームを除外する。
        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps) {
            return null;
        }

        // GetLeftPart(UriPartial.Path) はクエリとフラグメントを除いた部分を返す。
        // スキームとホストの小文字化、既定ポートの除去もこの時点で済んでいる。
        return absolute.GetLeftPart(UriPartial.Path) + NormalizeQuery(absolute.Query);
    }

    /// <summary>
    /// 計測用パラメータを除いたうえで、残ったパラメータを名前順に並べ直す。
    /// 残らなかった場合は空文字を返す。
    /// </summary>
    private static string NormalizeQuery(string query) {
        string joined = string.Join(
            '&',
            query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(parameter => !IsTracking(GetParameterName(parameter)))
                .OrderBy(GetParameterName, StringComparer.Ordinal));

        return joined.Length == 0 ? string.Empty : "?" + joined;
    }

    private static string GetParameterName(string parameter) {
        int separatorIndex = parameter.IndexOf('=', StringComparison.Ordinal);

        return separatorIndex < 0 ? parameter : parameter[..separatorIndex];
    }

    private static bool IsTracking(string parameterName) {
        return parameterName.StartsWith(TrackingParameterPrefix, StringComparison.OrdinalIgnoreCase)
        || TrackingParameterNames.Contains(parameterName);
    }
}
