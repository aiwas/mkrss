using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Crawling;

/// <summary>
/// クロール実行の結末。
/// </summary>
public enum CrawlRunOutcome {
    /// <summary>すべてのサイトを問題なく処理した。</summary>
    Succeeded,

    /// <summary>失敗したサイトがある、または実行自体が異常終了した。</summary>
    Failed,
}

/// <summary>
/// クロール実行における、サイト1件分の記録。
/// </summary>
/// <param name="SiteId">サイトの識別子。</param>
/// <param name="SiteName">サイト名。識別子だけでは対象が分からないため併せて返す。</param>
/// <param name="Outcome">
/// 結末。<see cref="CrawlOutcome"/> に対応する
/// <c>succeeded</c> / <c>fetch_failed</c> / <c>extraction_failed</c> /
/// <c>disallowed_by_robots</c> のいずれか。
/// </param>
/// <param name="ExtractedArticleCount">ページから抽出できた記事の件数。</param>
/// <param name="NewArticleCount">新しく保存された記事の件数。</param>
/// <param name="FailureReason">失敗した場合の理由。それ以外は <see langword="null"/>。</param>
public sealed record CrawlRunSiteStatus(
    Guid SiteId,
    string SiteName,
    string Outcome,
    int ExtractedArticleCount,
    int NewArticleCount,
    string? FailureReason) {
    /// <summary>
    /// 処理は成功したが、記事を1件も抽出できなかったかどうか。
    /// </summary>
    /// <remarks>
    /// セレクタが機能していないか、ページ構造が変わった可能性が高い。
    /// 例外にならないため結末は <c>succeeded</c> のまま記録され、この判定でしか見分けられない。
    /// 結末として保存せず、読み出すときに判定する。基準を変えたときに過去の記録まで追随させるため。
    /// </remarks>
    public bool ExtractedNothing => Outcome == "succeeded" && ExtractedArticleCount == 0;
}

/// <summary>
/// クロール実行の記録。
/// </summary>
/// <param name="Id">実行の識別子。</param>
/// <param name="StartedAt">開始日時。</param>
/// <param name="FinishedAt">終了日時。実行中の場合は <see langword="null"/>。</param>
/// <param name="Status">状態。<c>running</c> / <c>succeeded</c> / <c>failed</c> のいずれか。</param>
/// <param name="NewArticleCount">新しく保存された記事の件数。</param>
/// <param name="ErrorMessage">失敗の内容。それ以外は <see langword="null"/>。</param>
/// <param name="Sites">
/// サイトごとの内訳。実行中、またはサイトを1件も処理できなかった場合は空になる。
/// </param>
public sealed record CrawlRunStatus(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Status,
    int NewArticleCount,
    string? ErrorMessage,
    IReadOnlyList<CrawlRunSiteStatus> Sites) {
    /// <summary>
    /// 失敗は無いが、記事を抽出できなかったサイトを含むかどうか。
    /// </summary>
    /// <remarks>
    /// 失敗したサイトがある実行は対象外とする。失敗のほうが重く、そちらとして示せば足りるため。
    /// </remarks>
    public bool NeedsReview => Status == "succeeded" && Sites.Any(site => site.ExtractedNothing);
}

/// <summary>
/// クロール実行の記録を残し、読み出す。
/// </summary>
/// <remarks>
/// クロールはバックグラウンドで実行され、成否を呼び出し元へ返す経路が無い。
/// 結果はここに残し、管理UIの実行履歴で確認する。
/// 定期実行の次の時刻も、この記録の開始時刻から求める。
/// </remarks>
public interface ICrawlRunLog {
    /// <summary>
    /// 実行の開始を記録し、その識別子を返す。
    /// </summary>
    Task<Guid> StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 実行の終了を記録する。
    /// </summary>
    /// <param name="runId"><see cref="StartAsync"/> が返した識別子。</param>
    /// <param name="outcome">実行全体の結末。</param>
    /// <param name="report">
    /// サイトごとの結果。実行が異常終了してサイトを1件も処理できなかった場合は
    /// <see cref="CrawlReport.Empty"/> を渡す。
    /// </param>
    /// <param name="errorMessage">失敗の内容。それ以外は <see langword="null"/>。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <remarks>
    /// 全体の合計だけでは、どのサイトが機能していないかを後から追えない。
    /// 合計件数は <paramref name="report"/> から導けるため、別の引数では受け取らない。
    /// </remarks>
    Task FinishAsync(
        Guid runId,
        CrawlRunOutcome outcome,
        CrawlReport report,
        string? errorMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// 新しい順に、最大 <paramref name="limit"/> 件の実行の記録を返す。
    /// </summary>
    /// <param name="limit">返す件数の上限。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<IReadOnlyList<CrawlRunStatus>> ListRecentAsync(int limit, CancellationToken cancellationToken);
}
