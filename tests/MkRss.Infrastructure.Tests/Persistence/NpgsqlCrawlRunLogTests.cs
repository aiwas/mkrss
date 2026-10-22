using System;
using System.Collections.Generic;
using System.Linq;
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

        Guid runId = await log.StartAsync(CancellationToken.None);

        Assert.Equal("running", await ScalarAsync<string>("select status from crawl_runs"));

        CrawlRunStatus latest = await FindLatestAsync(log);

        Assert.Equal(runId, latest.Id);
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

        CrawlRunStatus latest = await FindLatestAsync(log);

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

        CrawlRunStatus latest = await FindLatestAsync(log);

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

        CrawlRunStatus latest = await FindLatestAsync(log);

        Assert.Equal("failed", latest.Status);
        Assert.NotNull(latest.FinishedAt);
        Assert.Empty(latest.Sites);
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsSiteBreakdown() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid runId = await log.StartAsync(CancellationToken.None);
        Guid siteId = await InsertSiteAsync();

        await log.FinishAsync(
            runId,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 3)]),
            null,
            CancellationToken.None);

        CrawlRunSiteStatus site = Assert.Single((await FindLatestAsync(log)).Sites);

        Assert.Equal(siteId, site.SiteId);

        // 識別子だけでは画面を見ても対象が分からないため、サイト名も併せて返す。
        Assert.Equal("お知らせ一覧", site.SiteName);
        Assert.Equal("succeeded", site.Outcome);
        Assert.Equal(10, site.ExtractedArticleCount);
        Assert.Equal(3, site.NewArticleCount);
        Assert.Null(site.FailureReason);
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsNewestFirstUpToLimit() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        Guid oldest = await StartAtAsync(log, "3 hours");
        Guid middle = await StartAtAsync(log, "2 hours");
        Guid newest = await StartAtAsync(log, "1 hour");

        IReadOnlyList<CrawlRunStatus> runs = await log.ListRecentAsync(2, CancellationToken.None);

        Assert.Equal([newest, middle], [.. runs.Select(run => run.Id)]);
        Assert.DoesNotContain(runs, run => run.Id == oldest);
    }

    [Fact]
    public async Task ListRecentAsync_EachRunHasItsOwnSiteBreakdown() {
        // 同じサイトが複数の実行に現れる。内訳を取り違えないこと。
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);
        Guid siteId = await InsertSiteAsync();

        Guid older = await StartAtAsync(log, "1 hour");
        await log.FinishAsync(
            older,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 1)]),
            null,
            CancellationToken.None);

        Guid newer = await log.StartAsync(CancellationToken.None);
        await log.FinishAsync(
            newer,
            CrawlRunOutcome.Succeeded,
            new CrawlReport([Succeeded(siteId, extracted: 10, newArticles: 2)]),
            null,
            CancellationToken.None);

        IReadOnlyList<CrawlRunStatus> runs = await log.ListRecentAsync(10, CancellationToken.None);

        Assert.Equal(2, runs.Count);
        Assert.Equal(2, Assert.Single(runs[0].Sites).NewArticleCount);
        Assert.Equal(1, Assert.Single(runs[1].Sites).NewArticleCount);
    }

    [Fact]
    public async Task ListRecentAsync_NoRuns_ReturnsEmpty() {
        NpgsqlCrawlRunLog log = new(Fixture.DataSource);

        Assert.Empty(await log.ListRecentAsync(10, CancellationToken.None));
    }

    private static async Task<CrawlRunStatus> FindLatestAsync(NpgsqlCrawlRunLog log) {
        return Assert.Single(await log.ListRecentAsync(1, CancellationToken.None));
    }

    /// <summary>
    /// 指定した時間だけ過去に開始した実行を記録する。
    /// </summary>
    /// <remarks>
    /// <c>started_at</c> の既定値は <c>now()</c> で、続けて記録すると順序が定まらないことがある。
    /// </remarks>
    private async Task<Guid> StartAtAsync(NpgsqlCrawlRunLog log, string ago) {
        Guid runId = await log.StartAsync(CancellationToken.None);

        await ExecuteAsync(
            "update crawl_runs set started_at = now() - $2::interval where id = $1", runId, ago);

        return runId;
    }

    private static SiteCrawlResult Succeeded(Guid siteId, int extracted, int newArticles) {
        return new SiteCrawlResult(siteId, CrawlOutcome.Succeeded, extracted, newArticles, null);
    }
}
