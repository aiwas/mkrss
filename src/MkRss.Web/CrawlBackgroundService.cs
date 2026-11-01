using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MkRss.Core.Crawling;
using MkRss.Infrastructure.Hosting;

namespace MkRss.Web;

/// <summary>
/// キューに入った実行要求を順に処理する。
/// </summary>
/// <remarks>
/// 要求は <see cref="CrawlScheduler"/> の定期実行と、管理UIからの手動実行の2か所から入る。
/// どちらから入っても、実行はここで1つずつ行う。
/// </remarks>
public sealed class CrawlBackgroundService : BackgroundService {
    private readonly CrawlQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrawlBackgroundService> _logger;

    public CrawlBackgroundService(
        CrawlQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<CrawlBackgroundService> logger) {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // 実行はここでしか行わないため、待ち受けを始める前なら実行中のものは無い。
        await FailInterruptedRunsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _queue.WaitForRequestAsync(stoppingToken);
            } catch (OperationCanceledException) {
                // 停止要求。ループを抜ける。
                break;
            }

            try {
                await RunAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            } catch (Exception exception) {
                // ここで受け止めないとバックグラウンド実行そのものが停止し、
                // 以降の要求が一切処理されなくなる。失敗自体は CrawlJob が記録済み。
                _logger.LogError(exception, "クロール実行失敗");
            } finally {
                _queue.Complete();
            }
        }
    }

    /// <summary>
    /// 前回のプロセスが終了を記録できずに残した実行を、失敗として片付ける。
    /// </summary>
    /// <remarks>
    /// 片付けられなくてもクロールは続ける。管理UIに「実行中」と残るだけで、実行には影響しないため。
    /// </remarks>
    private async Task FailInterruptedRunsAsync(CancellationToken cancellationToken) {
        try {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            ICrawlRunLog log = scope.ServiceProvider.GetRequiredService<ICrawlRunLog>();
            int count = await log.FailInterruptedAsync(cancellationToken);

            if (count > 0) {
                _logger.LogWarning("中断された実行の記録を失敗にした ({Count}件)", count);
            }
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            _logger.LogWarning(exception, "中断された実行の記録を片付けられなかった");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken) {
        // CrawlJob は DB 接続を伴うため、要求ごとにスコープを作る。
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        CrawlJob job = scope.ServiceProvider.GetRequiredService<CrawlJob>();
        CrawlReport report = await job.ExecuteAsync(cancellationToken);

        _logger.LogInformation(
            "クロール完了 (対象: {Sites}件、新規: {Articles}件)",
            report.Sites.Count,
            report.NewArticleCount);
    }
}
