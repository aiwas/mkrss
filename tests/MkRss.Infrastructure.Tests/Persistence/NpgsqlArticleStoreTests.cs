using System;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Extraction;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Persistence;

public sealed class NpgsqlArticleStoreTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

    private static readonly ExtractedArticle First = new(
        "最初のお知らせ",
        "https://example.com/news/first",
        new DateTimeOffset(2026, 9, 20, 10, 30, 0, Jst));

    private static readonly ExtractedArticle Second = new(
        "二番目のお知らせ",
        "https://example.com/news/second",
        new DateTimeOffset(2026, 9, 18, 9, 0, 0, Jst));

    private static readonly ExtractedArticle Third = new(
        "三番目のお知らせ",
        "https://example.com/news/third",
        new DateTimeOffset(2026, 9, 15, 12, 0, 0, Jst));

    [Fact]
    public async Task SaveNewAsync_ReturnsNumberOfArticlesActuallyAdded() {
        Guid siteId = await InsertSiteAsync();
        NpgsqlArticleStore store = new(Fixture.DataSource);

        int firstRun = await store.SaveNewAsync(siteId, [First, Second], CancellationToken.None);
        int secondRun = await store.SaveNewAsync(
            siteId, [First, Second, Third], CancellationToken.None);

        Assert.Equal(2, firstRun);
        Assert.Equal(1, secondRun);
        Assert.Equal(3, await ScalarAsync<long>("select count(*) from articles"));
    }

    [Fact]
    public async Task SaveNewAsync_NoArticles_ReturnsZero() {
        Guid siteId = await InsertSiteAsync();
        NpgsqlArticleStore store = new(Fixture.DataSource);

        Assert.Equal(0, await store.SaveNewAsync(siteId, [], CancellationToken.None));
    }

    [Fact]
    public async Task SaveNewAsync_StoresPublishedAtInUtc() {
        // 抽出時のオフセットに関わらず、DBにはUTCで入る。
        Guid siteId = await InsertSiteAsync();
        NpgsqlArticleStore store = new(Fixture.DataSource);

        await store.SaveNewAsync(siteId, [First], CancellationToken.None);

        DateTime stored = await ScalarAsync<DateTime>("select published_at from articles");

        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(new DateTime(2026, 9, 20, 1, 30, 0, DateTimeKind.Utc), stored);
    }

    [Fact]
    public async Task SaveNewAsync_StoresTitleAndUrl() {
        Guid siteId = await InsertSiteAsync();
        NpgsqlArticleStore store = new(Fixture.DataSource);

        await store.SaveNewAsync(siteId, [First], CancellationToken.None);

        Assert.Equal(First.Title, await ScalarAsync<string>("select title from articles"));
        Assert.Equal(First.Url, await ScalarAsync<string>("select normalized_url from articles"));
    }
}
