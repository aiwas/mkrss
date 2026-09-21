namespace MkRss.Core.Crawling;

/// <summary>
/// <see cref="Crawler"/> の動作設定。
/// </summary>
/// <param name="UserAgent">
/// robots.txt の User-agent グループを選ぶ際に使う名前。
/// ページ取得時に実際に送信する値と一致させること。
/// </param>
public sealed record CrawlerOptions(string UserAgent);
