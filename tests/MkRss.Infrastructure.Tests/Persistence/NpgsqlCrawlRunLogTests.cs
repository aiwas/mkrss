using System;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Persistence;

public sealed class NpgsqlCrawlRunLogTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    [Fact]
    public async Task StartAsync_LeavesRunningRow() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        await log.StartAsync(CancellationToken.None);

        Assert.Equal("running", await ScalarAsync<string>("select status from crawl_runs"));
        CrawlRunStatus? latest = await log.FindLatestAsync(CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Null(latest.FinishedAt);
    }

    [Fact]
    public async Task FinishAsync_Succeeded_RecordsCount() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);

        await log.FinishAsync(runId, CrawlRunOutcome.Succeeded, 7, null, CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal("succeeded", latest.Status);
        Assert.Equal(7, latest.NewArticleCount);
        Assert.Null(latest.ErrorMessage);
        Assert.NotNull(latest.FinishedAt);
    }

    [Fact]
    public async Task FinishAsync_Failed_RecordsErrorMessage() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);

        await log.FinishAsync(
            runId, CrawlRunOutcome.Failed, 0, "503 Service Unavailable", CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal("failed", latest.Status);
        Assert.Equal("503 Service Unavailable", latest.ErrorMessage);
    }

    [Fact]
    public async Task FindLatestAsync_ReturnsMostRecentRun() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        Guid older = await log.StartAsync(CancellationToken.None);
        await log.FinishAsync(older, CrawlRunOutcome.Succeeded, 1, null, CancellationToken.None);

        // started_at の既定値は now() で、同一トランザクション外なら順序が付く。
        await ExecuteAsync("update crawl_runs set started_at = now() - interval '1 hour'");

        Guid newer = await log.StartAsync(CancellationToken.None);
        await log.FinishAsync(newer, CrawlRunOutcome.Succeeded, 2, null, CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal(2, latest.NewArticleCount);
    }

    [Fact]
    public async Task FindLatestAsync_NoRuns_ReturnsNull() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        Assert.Null(await log.FindLatestAsync(CancellationToken.None));
    }
}
