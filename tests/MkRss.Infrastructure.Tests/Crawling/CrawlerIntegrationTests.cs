using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MkRss.Core.Crawling;
using MkRss.Core.Fetching;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Crawling;

/// <summary>
/// クロールの組み立て全体を、実際のDBに対して確認する。
/// </summary>
/// <remarks>
/// ページ取得だけを差し替え、巡回対象の読み出しと記事の保存は本物を使う。
/// </remarks>
public sealed class CrawlerIntegrationTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    private const string Html = """
        <!DOCTYPE html>
        <html lang="ja">
        <body>
          <ul class="news-list">
            <li class="news-item">
              <a href="/news/first"><span class="news-title">最初のお知らせ</span></a>
              <time class="news-date" datetime="2026-09-20T10:30:00+09:00">2026年9月20日</time>
            </li>
            <li class="news-item">
              <a href="/news/second?utm_source=mail"><span class="news-title">二番目のお知らせ</span></a>
              <time class="news-date" datetime="2026-09-18T09:00:00+09:00">2026年9月18日</time>
            </li>
          </ul>
        </body>
        </html>
        """;

    [Fact]
    public async Task RunAsync_SavesArticles_AndSecondRunAddsNothing() {
        await InsertSiteAsync();

        Crawler crawler = CreateCrawler();

        CrawlReport first = await crawler.RunAsync(CancellationToken.None);
        CrawlReport second = await crawler.RunAsync(CancellationToken.None);

        Assert.Equal(2, first.NewArticleCount);
        Assert.Equal(CrawlOutcome.Succeeded, Assert.Single(first.Sites).Outcome);

        // 2回目は増分が無い。中断しても次回の実行で回復できる前提がここで成立する。
        Assert.Equal(0, second.NewArticleCount);
        Assert.Equal(2, await ScalarAsync<long>("select count(*) from articles"));
    }

    [Fact]
    public async Task RunAsync_StoresNormalizedUrl() {
        await InsertSiteAsync();

        await CreateCrawler().RunAsync(CancellationToken.None);

        // 相対URLが解決され、計測用パラメータが除かれている。
        string stored = await ScalarAsync<string>(
            "select normalized_url from articles order by published_at desc offset 1");

        Assert.Equal("https://example.com/news/second", stored);
    }

    [Fact]
    public async Task RunAsync_DisabledSite_IsNotCrawled() {
        await InsertSiteAsync();
        await ExecuteAsync("update sites set is_enabled = false");

        CrawlReport report = await CreateCrawler().RunAsync(CancellationToken.None);

        Assert.Empty(report.Sites);
        Assert.Equal(0, await ScalarAsync<long>("select count(*) from articles"));
    }

    private Crawler CreateCrawler() {
        return new Crawler(
            new NpgsqlCrawlTargetSource(Fixture.DataSource),
            new StubPageFetcher(new Uri("https://example.com/news/"), Html),
            new NpgsqlArticleStore(Fixture.DataSource),
            new CrawlerOptions("mkrss/1.0"),
            NullLogger<Crawler>.Instance);
    }

    /// <summary>
    /// 指定したURLにだけ固定のHTMLを返す <see cref="IPageFetcher"/>。
    /// </summary>
    private sealed class StubPageFetcher(Uri pageUrl, string html) : IPageFetcher {
        private static readonly DateTimeOffset FetchedAt =
            new(2026, 9, 22, 3, 0, 0, TimeSpan.Zero);

        public Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            // robots.txt は登録していないため取得失敗となり、許可として扱われる。
            return Task.FromResult(url == pageUrl
                ? PageFetchResult.Success(html, FetchedAt)
                : PageFetchResult.Failure("登録されていないURL", FetchedAt));
        }
    }
}
