using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MkRss.Core.Crawling;
using MkRss.Core.Extraction;
using MkRss.Core.Tests.Fixtures;
using Xunit;

namespace MkRss.Core.Tests.Crawling;

public sealed class CrawlerTests {
    /// <summary>robots.txt の判定に使う product token。送信する User-Agent とは別の値。</summary>
    private const string ProductToken = "mkrss";

    private static readonly Uri PageUrl = new("https://example.com/news/");

    private static readonly Uri RobotsUrl = new("https://example.com/robots.txt");

    private static readonly ExtractionRule Rule = new(
        ItemSelector: ".news-item",
        Title: new FieldSelector(".news-title"),
        Link: new FieldSelector("a", "href"),
        Date: new FieldSelector(".news-date", "datetime"),
        DateOffset: TimeSpan.Zero);

    private static readonly CrawlTarget Target = new(
        Id: new Guid("11111111-1111-1111-1111-111111111111"),
        Name: "お知らせ一覧",
        PageUrl: PageUrl,
        Rule: Rule);

    [Fact]
    public async Task RunAsync_SavesExtractedArticles() {
        FakePageFetcher fetcher = new();
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        CrawlReport report = await CreateCrawler(fetcher, store, Target).RunAsync(CancellationToken.None);

        (Guid siteId, var articles) = Assert.Single(store.Saved);

        Assert.Equal(Target.Id, siteId);
        Assert.Equal(
            ["最初のお知らせ", "二番目のお知らせ", "三番目のお知らせ"],
            articles.Select(article => article.Title));
        Assert.Equal(3, report.NewArticleCount);
    }

    [Fact]
    public async Task RunAsync_Succeeded_RecordsExtractedCountSeparatelyFromNewCount() {
        // 抽出できた件数と新規保存された件数を分けて持つ。
        // 新規0件が「更新が無かった」のか「セレクタが機能しておらず抽出0件」なのかを
        // 区別できないと、そのサイトだけ止まっていることに気づけない。
        FakePageFetcher fetcher = new();
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new(newArticleCount: 1);

        CrawlReport report = await CreateCrawler(fetcher, store, Target).RunAsync(CancellationToken.None);

        SiteCrawlResult site = Assert.Single(report.Sites);

        Assert.Equal(3, site.ExtractedArticleCount);
        Assert.Equal(1, site.NewArticleCount);
    }

    [Fact]
    public async Task RunAsync_ShiftJisPageDeclaredOnlyInMeta_SavesReadableTitles() {
        // 取得から抽出までを通した確認。取得側が復号すると、この宣言は読めない。
        // 文字化けしたまま保存すると、増分挿入(ON CONFLICT DO NOTHING)のため
        // 後から実装を直しても、その行は更新されない。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] page = Encoding.GetEncoding("shift_jis").GetBytes("""
            <!DOCTYPE html>
            <html lang="ja">
            <head><meta charset="Shift_JIS"></head>
            <body>
              <div class="news-item">
                <span class="news-title">最初のお知らせ</span>
                <a href="/news/first">続きを読む</a>
                <time class="news-date" datetime="2026-09-20T10:30:00+09:00">2026年9月20日</time>
              </div>
            </body>
            </html>
            """);

        FakePageFetcher fetcher = new();
        fetcher.SetContent(PageUrl, page, declaredCharset: null);
        FakeArticleStore store = new();

        await CreateCrawler(fetcher, store, Target).RunAsync(CancellationToken.None);

        (_, var articles) = Assert.Single(store.Saved);

        Assert.Equal("最初のお知らせ", Assert.Single(articles).Title);
    }

    [Fact]
    public async Task RunAsync_DisallowedByRobots_DoesNotFetchPage() {
        FakePageFetcher fetcher = new();
        fetcher.SetContent(RobotsUrl, "User-agent: *\nDisallow: /news/\n");
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        CrawlReport report = await CreateCrawler(fetcher, store, Target).RunAsync(CancellationToken.None);

        Assert.Equal(CrawlOutcome.DisallowedByRobots, Assert.Single(report.Sites).Outcome);
        Assert.Empty(store.Saved);
        Assert.DoesNotContain(PageUrl, fetcher.RequestedUrls);
    }

    [Fact]
    public async Task RunAsync_TargetsOnSameHost_FetchesRobotsOnce() {
        CrawlTarget second = Target with {
            Id = new Guid("22222222-2222-2222-2222-222222222222"),
            Name = "投稿一覧",
            PageUrl = new Uri("https://example.com/posts/"),
        };

        FakePageFetcher fetcher = new();
        fetcher.SetContent(RobotsUrl, "User-agent: *\nDisallow: /private\n");
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        fetcher.SetContent(second.PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        await CreateCrawler(fetcher, store, Target, second).RunAsync(CancellationToken.None);

        Assert.Equal(1, fetcher.RequestedUrls.Count(url => url == RobotsUrl));
    }

    [Fact]
    public async Task RunAsync_SameHostWithDifferentScheme_UsesItsOwnRobots() {
        // RFC 9309 は robots.txt の適用範囲を origin(スキーム + ホスト + ポート)と定めている。
        // ホストだけでキャッシュすると、http と https で異なる内容を取り違える。
        CrawlTarget insecure = Target with {
            Id = new Guid("55555555-5555-5555-5555-555555555555"),
            Name = "http側の一覧",
            PageUrl = new Uri("http://example.com/news/"),
        };

        FakePageFetcher fetcher = new();
        fetcher.SetContent(RobotsUrl, "User-agent: *\nDisallow: /private\n");
        fetcher.SetContent(
            new Uri("http://example.com/robots.txt"), "User-agent: *\nDisallow: /news/\n");
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        fetcher.SetContent(insecure.PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        CrawlReport report = await CreateCrawler(fetcher, store, Target, insecure)
            .RunAsync(CancellationToken.None);

        Assert.Equal(CrawlOutcome.Succeeded, report.Sites[0].Outcome);
        Assert.Equal(CrawlOutcome.DisallowedByRobots, report.Sites[1].Outcome);
    }

    [Fact]
    public async Task RunAsync_FetchFailure_ContinuesWithRemainingTargets() {
        // 1サイトの障害で全サイトのフィード更新が止まらないようにする。
        CrawlTarget failing = Target with {
            Id = new Guid("33333333-3333-3333-3333-333333333333"),
            Name = "取得できないサイト",
            PageUrl = new Uri("https://broken.example/news/"),
        };

        FakePageFetcher fetcher = new();
        fetcher.SetFailure(failing.PageUrl, "503 Service Unavailable");
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        CrawlReport report = await CreateCrawler(fetcher, store, failing, Target)
            .RunAsync(CancellationToken.None);

        Assert.Equal(CrawlOutcome.FetchFailed, report.Sites[0].Outcome);
        Assert.Equal("503 Service Unavailable", report.Sites[0].FailureReason);
        Assert.Equal(0, report.Sites[0].ExtractedArticleCount);
        Assert.Equal(CrawlOutcome.Succeeded, report.Sites[1].Outcome);
        Assert.Equal(3, report.NewArticleCount);
        Assert.True(report.HasFailure);
    }

    [Fact]
    public async Task RunAsync_ExtractionThrows_ContinuesWithRemainingTargets() {
        // セレクタの構文はDB登録時に検証しない(決定事項1)ため、壊れた規則が登録されうる。
        CrawlTarget broken = Target with {
            Id = new Guid("44444444-4444-4444-4444-444444444444"),
            Name = "規則が壊れたサイト",
            PageUrl = new Uri("https://example.org/news/"),
            Rule = Rule with { ItemSelector = "((" },
        };

        FakePageFetcher fetcher = new();
        fetcher.SetContent(broken.PageUrl, HtmlFixture.Load("news-listing.html"));
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        CrawlReport report = await CreateCrawler(fetcher, store, broken, Target)
            .RunAsync(CancellationToken.None);

        Assert.Equal(CrawlOutcome.ExtractionFailed, report.Sites[0].Outcome);
        Assert.NotNull(report.Sites[0].FailureReason);
        Assert.Equal(0, report.Sites[0].ExtractedArticleCount);
        Assert.Equal(CrawlOutcome.Succeeded, report.Sites[1].Outcome);
        Assert.Equal(3, report.NewArticleCount);
    }

    [Fact]
    public async Task RunAsync_Cancelled_PropagatesCancellation() {
        // キャンセルはサイトの失敗として握り潰さず、呼び出し側へ伝える。
        FakePageFetcher fetcher = new();
        fetcher.SetContent(PageUrl, HtmlFixture.Load("news-listing.html"));
        FakeArticleStore store = new();

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateCrawler(fetcher, store, Target).RunAsync(cancellation.Token));
    }

    private static Crawler CreateCrawler(
        FakePageFetcher fetcher,
        FakeArticleStore store,
        params CrawlTarget[] targets) {
        return new Crawler(
            new FakeCrawlTargetSource(targets),
            fetcher,
            store,
            new CrawlerOptions(ProductToken),
            NullLogger<Crawler>.Instance);
    }
}
