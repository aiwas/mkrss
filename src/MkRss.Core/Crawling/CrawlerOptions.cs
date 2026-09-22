namespace MkRss.Core.Crawling;

/// <summary>
/// <see cref="Crawler"/> の動作設定。
/// </summary>
/// <param name="ProductToken">
/// robots.txt の User-agent グループを選ぶ際に使う product token(例 : <c>mkrss</c>)。
/// </param>
/// <remarks>
/// <para>
/// <b>ページ取得時に送信する User-Agent ヘッダとは別の値。</b>
/// RFC 9309 は product token を英字・アンダースコア・ハイフンのみと定めており、
/// <c>mkrss/1.0 (+https://example.net/about)</c> のような完全な User-Agent 文字列は
/// product token として成立しない。両者を同じ値にすると、
/// <c>User-agent: mkrss</c> と名指しで指定されたグループに一致せず、
/// そのサイトの意思を無視して巡回することになる。
/// </para>
/// <para>
/// 送信する User-Agent は <c>MkRss.Infrastructure.Fetching.PageFetcherOptions</c> が持つ。
/// </para>
/// </remarks>
public sealed record CrawlerOptions(string ProductToken);
