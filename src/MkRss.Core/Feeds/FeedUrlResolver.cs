using System;

namespace MkRss.Core.Feeds;

/// <summary>
/// フィード自身のURL(Atomの <c>rel="self"</c>)を決める。
/// </summary>
public static class FeedUrlResolver {
    /// <summary>
    /// 公開URLを組み立てる。
    /// </summary>
    /// <param name="publicBaseUrl">
    /// 設定された公開URLの基底。<see langword="null"/> または空の場合は
    /// <paramref name="requestUrl"/> をそのまま使う。
    /// </param>
    /// <param name="requestUrl">アプリが受け取ったリクエストのURL。</param>
    /// <param name="pathAndQuery">リクエストのパスとクエリ。</param>
    /// <remarks>
    /// リバースプロキシの背後ではリクエストのスキームが <c>http</c> になり、
    /// 転送ヘッダを信頼する設定を入れない限り <c>https</c> に戻らない。
    /// プロキシが多段になるほどこの連鎖は壊れやすいため、
    /// 公開URLが決まっているなら設定で与えるほうが確実。
    /// </remarks>
    public static Uri Resolve(string? publicBaseUrl, Uri requestUrl, string pathAndQuery) {
        if (string.IsNullOrEmpty(publicBaseUrl)) {
            return requestUrl;
        }

        return new Uri(new Uri(publicBaseUrl), pathAndQuery);
    }
}
