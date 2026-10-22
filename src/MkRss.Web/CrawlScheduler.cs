using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MkRss.Core.Crawling;
using MkRss.Infrastructure.Hosting;

namespace MkRss.Web;

/// <summary>
/// 定期クロールの時刻になったら実行を要求する。
/// </summary>
/// <remarks>
/// <para>
/// 実行そのものは <see cref="CrawlBackgroundService"/> が行う。ここは <see cref="CrawlQueue"/> に
/// 要求を入れるだけで、管理UIからの手動実行と同じ経路を通る。多重実行の防止もそちらに任せる。
/// </para>
/// <para>
/// 次の時刻は <see cref="CrawlSchedule"/> がDBの記録から求める。
/// 待機から戻るたびに記録を読み直すため、待機中に手動で実行された場合はその分だけ後ろへずれる。
/// </para>
/// </remarks>
public sealed class CrawlScheduler : BackgroundService {
    private readonly CrawlSchedule _schedule;
    private readonly CrawlQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CrawlScheduler> _logger;

    public CrawlScheduler(
        CrawlSchedule schedule,
        CrawlQueue queue,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<CrawlScheduler> logger) {
        _schedule = schedule;
        _queue = queue;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // 要求を入れた時刻。DBの記録だけを見ると、実行が始まって記録されるまでの間や、
        // DBに接続できず記録が残らない場合に「すぐ実行」と判断し続け、要求を繰り返してしまう。
        DateTimeOffset? requestedAt = null;

        try {
            while (!stoppingToken.IsCancellationRequested) {
                DateTimeOffset? recordedAt = await FindLastStartedAtAsync(stoppingToken);

                DateTimeOffset now = _timeProvider.GetUtcNow();
                DateTimeOffset next = _schedule.NextRunAt(Latest(recordedAt, requestedAt), now);

                if (next > now) {
                    _logger.LogInformation("次のクロールは {NextRunAt} に実行する。", next);

                    await Task.Delay(next - now, _timeProvider, stoppingToken);

                    continue;
                }

                if (!_queue.TryEnqueue()) {
                    // 手動で実行されたものが走っている。それを今回の実行とみなす。
                    _logger.LogInformation("クロールが実行中のため、定期実行を見送った。");
                }

                requestedAt = now;
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // 停止要求。
        }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) {
        if (first is null) {
            return second;
        }

        return second is null || first > second ? first : second;
    }

    /// <summary>
    /// 直近の実行の開始時刻を読む。記録が無い、または読めない場合は <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 読めない場合に例外を伝えると、ホストごと停止する。
    /// 記録が無いものとして扱い、クロールの側で失敗として記録させる。
    /// </remarks>
    private async Task<DateTimeOffset?> FindLastStartedAtAsync(CancellationToken cancellationToken) {
        try {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            ICrawlRunLog log = scope.ServiceProvider.GetRequiredService<ICrawlRunLog>();

            return (await log.ListRecentAsync(1, cancellationToken)).FirstOrDefault()?.StartedAt;
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            _logger.LogWarning(exception, "直近のクロールの記録を読めなかった。");

            return null;
        }
    }
}
