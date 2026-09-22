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
        Guid siteId = await InsertSiteAsync();

        await log.FinishAsync(
            runId,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 7)]),
            null,
            CancellationToken.None);

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
            runId,
            CrawlRunOutcome.Failed,
            CrawlReport.Empty,
            "503 Service Unavailable",
            CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal("failed", latest.Status);
        Assert.Equal("503 Service Unavailable", latest.ErrorMessage);
    }

    [Fact]
    public async Task FinishAsync_RecordsResultOfEverySite() {
        // 全体の合計だけでは、どのサイトが機能していないかを後から追えない。
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);
        Guid succeeded = await InsertSiteAsync("example-com-news-a1b2c3d4");
        Guid failed = await InsertSiteAsync("broken-example-news-e5f6a7b8");

        await log.FinishAsync(
            runId,
            CrawlRunOutcome.Failed,
            new CrawlReport([
                Succeeded(succeeded, extracted: 10, newArticles: 3),
                new SiteCrawlResult(failed, CrawlOutcome.FetchFailed, 0, 0, "503 Service Unavailable"),
            ]),
            "2 件中 1 件が失敗した。",
            CancellationToken.None);

        Assert.Equal(
            2L,
            await ScalarAsync<long>("select count(*) from crawl_run_sites where crawl_run_id = $1", runId));

        Assert.Equal(
            "succeeded",
            await ScalarAsync<string>(
                "select outcome from crawl_run_sites where site_id = $1", succeeded));

        Assert.Equal(
            "fetch_failed",
            await ScalarAsync<string>(
                "select outcome from crawl_run_sites where site_id = $1", failed));
    }

    [Fact]
    public async Task FinishAsync_NoSiteResults_StillFinishesRun() {
        // 巡回対象の読み出し自体に失敗した場合、サイトごとの結果は1件も無い。
        // それでも実行の終了は記録しないと、status が running のまま残る。
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);

        await log.FinishAsync(
            runId, CrawlRunOutcome.Failed, CrawlReport.Empty, "DBに接続できない", CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal("failed", latest.Status);
        Assert.NotNull(latest.FinishedAt);
        Assert.Empty(latest.Sites);
    }

    [Fact]
    public async Task FindLatestAsync_ReturnsSiteBreakdown() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);
        Guid siteId = await InsertSiteAsync();

        await log.FinishAsync(
            runId,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 3)]),
            null,
            CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        CrawlRunSiteStatus site = Assert.Single(latest.Sites);

        Assert.Equal(siteId, site.SiteId);

        // 識別子だけでは応答を読んでも対象が分からないため、サイト名も併せて返す。
        Assert.Equal("お知らせ一覧", site.SiteName);
        Assert.Equal("succeeded", site.Outcome);
        Assert.Equal(10, site.ExtractedArticleCount);
        Assert.Equal(3, site.NewArticleCount);
        Assert.Null(site.FailureReason);
    }

    [Fact]
    public async Task FindLatestAsync_ReturnsMostRecentRun() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid siteId = await InsertSiteAsync();

        Guid older = await log.StartAsync(CancellationToken.None);
        await log.FinishAsync(
            older,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 1)]),
            null,
            CancellationToken.None);

        // started_at の既定値は now() で、同一トランザクション外なら順序が付く。
        await ExecuteAsync("update crawl_runs set started_at = now() - interval '1 hour'");

        Guid newer = await log.StartAsync(CancellationToken.None);
        await log.FinishAsync(
            newer,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 2)]),
            null,
            CancellationToken.None);

        CrawlRunStatus latest = Assert.IsType<CrawlRunStatus>(
            await log.FindLatestAsync(CancellationToken.None));

        Assert.Equal(2, latest.NewArticleCount);

        // 内訳も直近の実行のものだけを返す。同じサイトが過去の実行にも現れるため。
        Assert.Equal(2, Assert.Single(latest.Sites).NewArticleCount);
    }

    [Fact]
    public async Task FindLatestAsync_NoRuns_ReturnsNull() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        Assert.Null(await log.FindLatestAsync(CancellationToken.None));
    }

    private static SiteCrawlResult Succeeded(Guid siteId, int extracted, int newArticles) {
        return new SiteCrawlResult(siteId, CrawlOutcome.Succeeded, extracted, newArticles, null);
    }
}
