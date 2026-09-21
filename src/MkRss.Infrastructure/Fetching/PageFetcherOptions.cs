using System;

namespace MkRss.Infrastructure.Fetching;

/// <summary>
/// <see cref="HttpPageFetcher"/> の動作設定。
/// </summary>
/// <param name="UserAgent">
/// 送信する User-Agent。巡回元を識別できる文字列を指定する。
/// </param>
/// <param name="Timeout">1ページあたりの取得を諦めるまでの時間。</param>
public sealed record PageFetcherOptions(string UserAgent, TimeSpan Timeout);
