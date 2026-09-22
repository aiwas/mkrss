using System;

namespace MkRss.Infrastructure.Fetching;

/// <summary>
/// <see cref="HttpPageFetcher"/> の動作設定。
/// </summary>
/// <param name="UserAgent">
/// 送信する User-Agent。巡回元を識別できる文字列を指定する。
/// </param>
/// <param name="Timeout">1ページあたりの取得を諦めるまでの時間。</param>
/// <param name="MaxPageBytes">
/// 1ページあたりに読み込むバイト数の上限。超えた場合は取得の失敗として扱う。
/// 巡回先が巨大な応答を返したときに、タイムアウト内であってもメモリを使い切らないようにする。
/// </param>
public sealed record PageFetcherOptions(string UserAgent, TimeSpan Timeout, int MaxPageBytes);
