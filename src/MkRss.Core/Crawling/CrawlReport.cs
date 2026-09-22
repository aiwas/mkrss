using System;
using System.Collections.Generic;
using System.Linq;

namespace MkRss.Core.Crawling;

/// <summary>
/// 1サイトを処理した結末。
/// </summary>
public enum CrawlOutcome {
    /// <summary>取得・抽出・保存まで到達した。</summary>
    Succeeded,

    /// <summary>ページを取得できなかった。</summary>
    FetchFailed,

    /// <summary>取得はできたが、抽出規則を適用できなかった。</summary>
    ExtractionFailed,

    /// <summary>robots.txt により取得が許可されていない。</summary>
    DisallowedByRobots,
}

/// <summary>
/// 1サイトの処理結果。
/// </summary>
/// <param name="SiteId">対象サイトの識別子。</param>
/// <param name="Outcome">結末。</param>
/// <param name="ExtractedArticleCount">
/// ページから抽出できた記事の件数。既に保存済みのものも含む。
/// </param>
/// <param name="NewArticleCount">新しく保存された記事の件数。</param>
/// <param name="FailureReason">失敗した場合の理由。それ以外は <see langword="null"/>。</param>
/// <remarks>
/// 抽出件数と新規件数を分けて持つ。新規0件だけでは、更新が無かったのか、
/// セレクタが機能しておらず抽出そのものが0件なのかを区別できないため。
/// 後者は例外にならず(決定事項1)、この2つの値の差としてのみ現れる。
/// </remarks>
public sealed record SiteCrawlResult(
    Guid SiteId,
    CrawlOutcome Outcome,
    int ExtractedArticleCount,
    int NewArticleCount,
    string? FailureReason);

/// <summary>
/// 1回のクロール全体の結果。
/// </summary>
/// <param name="Sites">処理したサイトごとの結果。巡回した順に並ぶ。</param>
public sealed record CrawlReport(IReadOnlyList<SiteCrawlResult> Sites) {
    /// <summary>
    /// サイトを1件も処理できなかった実行の結果。
    /// </summary>
    public static CrawlReport Empty { get; } = new([]);

    /// <summary>新しく保存された記事の合計件数。</summary>
    public int NewArticleCount => Sites.Sum(site => site.NewArticleCount);

    /// <summary>失敗したサイトがあるかどうか。robots.txt による除外は失敗に数えない。</summary>
    public bool HasFailure => Sites.Any(site => site.FailureReason is not null);
}
