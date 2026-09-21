using System;
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
/// クロール実行の記録。
/// </summary>
/// <param name="StartedAt">開始日時。</param>
/// <param name="FinishedAt">終了日時。実行中の場合は <see langword="null"/>。</param>
/// <param name="Status">状態。<c>running</c> / <c>succeeded</c> / <c>failed</c> のいずれか。</param>
/// <param name="NewArticleCount">新しく保存された記事の件数。</param>
/// <param name="ErrorMessage">失敗の内容。それ以外は <see langword="null"/>。</param>
public sealed record CrawlRunStatus(
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Status,
    int NewArticleCount,
    string? ErrorMessage);

/// <summary>
/// クロール実行の記録を残す。
/// </summary>
/// <remarks>
/// CRONは <c>POST /api/crawl</c> の応答でクロールの成否を知ることができない。
/// exit code による失敗検知の代替として、実行結果をここに残す。
/// </remarks>
public interface ICrawlRunLog {
    /// <summary>
    /// 実行の開始を記録し、その識別子を返す。
    /// </summary>
    Task<Guid> StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 実行の終了を記録する。
    /// </summary>
    Task FinishAsync(
        Guid runId,
        CrawlRunOutcome outcome,
        int newArticleCount,
        string? errorMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// 直近の実行の記録を返す。1件も無い場合は <see langword="null"/>。
    /// </summary>
    Task<CrawlRunStatus?> FindLatestAsync(CancellationToken cancellationToken);
}
