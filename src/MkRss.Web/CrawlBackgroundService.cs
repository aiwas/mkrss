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
/// HTTPリクエスト内でクロールを完走させると、リバースプロキシのタイムアウトにより
/// CRON側が失敗と判断し、再実行で多重クロールが発生する。そのためここで実行する。
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
                _logger.LogError(exception, "クロールの実行が異常終了した。");
            } finally {
                _queue.Complete();
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken) {
        // CrawlJob は DB 接続を伴うため、要求ごとにスコープを作る。
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        CrawlJob job = scope.ServiceProvider.GetRequiredService<CrawlJob>();
        CrawlReport report = await job.ExecuteAsync(cancellationToken);

        _logger.LogInformation(
            "クロールが完了した。対象 {Sites} 件、新規 {Articles} 件。",
            report.Sites.Count,
            report.NewArticleCount);
    }
}
