using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MkRss.Core.Crawling;

/// <summary>
/// クロールを1回実行し、その結果を記録する。
/// </summary>
public sealed class CrawlJob {
    private readonly Crawler _crawler;
    private readonly ICrawlRunLog _log;
    private readonly ILogger<CrawlJob> _logger;

    public CrawlJob(Crawler crawler, ICrawlRunLog log, ILogger<CrawlJob> logger) {
        _crawler = crawler;
        _log = log;
        _logger = logger;
    }

    /// <summary>
    /// クロールを実行する。
    /// </summary>
    /// <remarks>
    /// 想定外の例外は記録したうえで呼び出し側へ伝える。
    /// 記録を残さないと状態が <c>running</c> のまま残り、次回以降の判断ができなくなる。
    /// </remarks>
    public async Task<CrawlReport> ExecuteAsync(CancellationToken cancellationToken) {
        Guid runId = await _log.StartAsync(cancellationToken);

        CrawlReport report;

        try {
            report = await _crawler.RunAsync(cancellationToken);
        } catch (Exception exception) {
            _logger.LogError(exception, "クロールが異常終了した。");

            // 中断された場合も記録する。ここでトークンを使うと記録自体が中断される。
            await _log.FinishAsync(
                runId, CrawlRunOutcome.Failed, 0, exception.Message, CancellationToken.None);

            throw;
        }

        string? failures = DescribeFailures(report);

        await _log.FinishAsync(
            runId,
            failures is null ? CrawlRunOutcome.Succeeded : CrawlRunOutcome.Failed,
            report.NewArticleCount,
            failures,
            cancellationToken);

        return report;
    }

    /// <summary>
    /// 失敗したサイトの内容をまとめる。失敗が無い場合は <see langword="null"/> を返す。
    /// </summary>
    /// <remarks>
    /// robots.txt による除外は失敗として扱わない。設定どおりの動作であるため。
    /// </remarks>
    private static string? DescribeFailures(CrawlReport report) {
        List<string> failures = [.. report.Sites
            .Where(site => site.FailureReason is not null)
            .Select(site => $"{site.SiteId}: {site.FailureReason}")];

        return failures.Count == 0
            ? null
            : $"{report.Sites.Count} 件中 {failures.Count} 件が失敗した。{string.Join(" / ", failures)}";
    }
}
