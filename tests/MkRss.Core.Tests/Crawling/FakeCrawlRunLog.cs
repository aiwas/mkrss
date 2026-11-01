using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;

namespace MkRss.Core.Tests.Crawling;

/// <summary>
/// 記録内容を保持するだけの <see cref="ICrawlRunLog"/>。
/// </summary>
internal sealed class FakeCrawlRunLog : ICrawlRunLog {
    public Guid RunId { get; } = new("99999999-9999-9999-9999-999999999999");

    /// <summary>開始が記録された回数。</summary>
    public int StartedCount { get; private set; }

    /// <summary>終了時に記録された内容。未終了の場合は <see langword="null"/>。</summary>
    public (CrawlRunOutcome Outcome, int NewArticleCount, IReadOnlyList<SiteCrawlResult> Sites, string? ErrorMessage)? Finished { get; private set; }

    public Task<Guid> StartAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        StartedCount++;

        return Task.FromResult(RunId);
    }

    public Task FinishAsync(
        Guid runId,
        CrawlRunOutcome outcome,
        CrawlReport report,
        string? errorMessage,
        CancellationToken cancellationToken) {
        if (runId != RunId) {
            throw new InvalidOperationException($"開始していない実行の終了が記録された: {runId}");
        }

        Finished = (outcome, report.NewArticleCount, report.Sites, errorMessage);

        return Task.CompletedTask;
    }

    public Task<int> FailInterruptedAsync(CancellationToken cancellationToken) {
        throw new NotSupportedException("このテストでは使用しない。");
    }

    public Task<IReadOnlyList<CrawlRunStatus>> ListRecentAsync(int limit, CancellationToken cancellationToken) {
        throw new NotSupportedException("このテストでは使用しない。");
    }
}
