using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Feeds;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Persistence;

public sealed class NpgsqlFeedSourceTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    private const string Slug = "example-com-news-a1b2c3d4";

    [Fact]
    public async Task FindBySlugAsync_UnknownSlug_ReturnsNull() {
        await InsertSiteAsync(Slug);

        Assert.Null(await FindAsync("does-not-exist", 50));
    }

    [Fact]
    public async Task FindBySlugAsync_ReturnsSiteInformation() {
        Guid siteId = await InsertSiteAsync(Slug);

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 50));

        Assert.Equal(siteId, feed.Site.Id);
        Assert.Equal("お知らせ一覧", feed.Site.Name);
        Assert.Equal("https://example.com/news/", feed.Site.PageUrl.AbsoluteUri);
    }

    [Fact]
    public async Task FindBySlugAsync_OrdersEntriesNewestFirst() {
        Guid siteId = await InsertSiteAsync(Slug);
        await InsertArticleAsync(siteId, "https://example.com/news/old", "古い記事", "2026-09-10T00:00:00Z");
        await InsertArticleAsync(siteId, "https://example.com/news/new", "新しい記事", "2026-09-20T00:00:00Z");

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 50));

        Assert.Equal(["新しい記事", "古い記事"], feed.Entries.Select(entry => entry.Title));
    }

    [Fact]
    public async Task FindBySlugAsync_AppliesLimit() {
        Guid siteId = await InsertSiteAsync(Slug);
        await InsertArticleAsync(siteId, "https://example.com/news/1", "1件目", "2026-09-10T00:00:00Z");
        await InsertArticleAsync(siteId, "https://example.com/news/2", "2件目", "2026-09-20T00:00:00Z");

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 1));

        Assert.Equal("2件目", Assert.Single(feed.Entries).Title);
    }

    [Fact]
    public async Task FindBySlugAsync_NoArticles_ReturnsEmptyEntries() {
        // 記事が無いサイトでも、フィード自体は配信できる必要がある。
        await InsertSiteAsync(Slug);

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 50));

        Assert.Empty(feed.Entries);
    }

    [Fact]
    public async Task FindBySlugAsync_DisabledSite_IsStillServed() {
        // is_enabled は巡回するかどうかの設定であり、配信を止める意味ではない。
        Guid siteId = await InsertSiteAsync(Slug);
        await InsertArticleAsync(siteId, "https://example.com/news/1", "記事", "2026-09-20T00:00:00Z");
        await ExecuteAsync("update sites set is_enabled = false");

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 50));

        Assert.Single(feed.Entries);
    }

    [Fact]
    public async Task FindBySlugAsync_ReturnsPublishedAtInUtc() {
        Guid siteId = await InsertSiteAsync(Slug);
        await InsertArticleAsync(siteId, "https://example.com/news/1", "記事", "2026-09-20T10:30:00+09:00");

        FeedData feed = Assert.IsType<FeedData>(await FindAsync(Slug, 50));

        FeedEntry entry = Assert.Single(feed.Entries);

        Assert.Equal(TimeSpan.Zero, entry.PublishedAt.Offset);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 20, 1, 30, 0, TimeSpan.Zero), entry.PublishedAt);
    }

    private async Task InsertArticleAsync(Guid siteId, string url, string title, string publishedAt) {
        await ExecuteAsync(
            """
            insert into articles (site_id, normalized_url, title, published_at)
            values ($1, $2, $3, $4::timestamptz)
            """,
            siteId,
            url,
            title,
            publishedAt);
    }

    private async Task<FeedData?> FindAsync(string slug, int limit) {
        return await new NpgsqlFeedSource(Fixture.DataSource)
            .FindBySlugAsync(slug, limit, CancellationToken.None);
    }
}
