using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MkRss.Core.Crawling;
using MkRss.Core.Extraction;
using MkRss.Core.Tests.Fixtures;
using Xunit;

namespace MkRss.Core.Tests.Crawling;

public sealed class CrawlJobTests {
    private static readonly Uri PageUrl = new("https://example.com/news/");

    private static readonly CrawlTarget Target = new(
        Id: new Guid("11111111-1111-1111-1111-111111111111"),
        Name: "お知らせ一覧",
        PageUrl: PageUrl,
        Rule: new ExtractionRule(
            ItemSelector: ".news-item",
            Title: new FieldSelector(".news-title"),
            Link: new FieldSelector("a", "href"),
            Date: new FieldSelector(".news-date", "datetime"),
            DateOffset: TimeSpan.Zero));

    [Fact]
    public async Task ExecuteAsync_Succeeded_RecordsNewArticleCount() {
        FakePageFetcher fetcher = new();
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeCrawlRunLog log = new();

        CrawlReport report = await CreateJob(fetcher, log).ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, log.StartedCount);
        Assert.Equal((CrawlRunOutcome.Succeeded, 3, null), log.Finished);
        Assert.Equal(3, report.NewArticleCount);
    }

    [Fact]
    public async Task ExecuteAsync_SiteFailed_RecordsFailure() {
        // 1サイトでも取得に失敗したら、実行全体としては失敗として残す。
        // CRONはHTTPの応答で成否を判断できないため、この記録が唯一の手掛かりになる。
        FakePageFetcher fetcher = new();
        fetcher.SetFailure(PageUrl, "503 Service Unavailable");
        FakeCrawlRunLog log = new();

        await CreateJob(fetcher, log).ExecuteAsync(CancellationToken.None);

        Assert.NotNull(log.Finished);
        Assert.Equal(CrawlRunOutcome.Failed, log.Finished.Value.Outcome);
        Assert.Contains("503", log.Finished.Value.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedException_RecordsFailureAndRethrows() {
        FakeCrawlRunLog log = new();
        CrawlJob job = new(
            new Crawler(
                new ThrowingCrawlTargetSource(),
                new FakePageFetcher(),
                new FakeArticleStore(),
                new CrawlerOptions("mkrss/1.0"),
                NullLogger<Crawler>.Instance),
            log,
            NullLogger<CrawlJob>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.ExecuteAsync(CancellationToken.None));

        Assert.NotNull(log.Finished);
        Assert.Equal(CrawlRunOutcome.Failed, log.Finished.Value.Outcome);
    }

    private static CrawlJob CreateJob(FakePageFetcher fetcher, FakeCrawlRunLog log) {
        return new CrawlJob(
            new Crawler(
                new FakeCrawlTargetSource(Target),
                fetcher,
                new FakeArticleStore(),
                new CrawlerOptions("mkrss/1.0"),
                NullLogger<Crawler>.Instance),
            log,
            NullLogger<CrawlJob>.Instance);
    }

    /// <summary>巡回対象の読み出しそのものが失敗する状況を作る。</summary>
    private sealed class ThrowingCrawlTargetSource : ICrawlTargetSource {
        public Task<IReadOnlyList<CrawlTarget>> ListEnabledAsync(CancellationToken cancellationToken) {
            throw new InvalidOperationException("DBに接続できない");
        }
    }
}
