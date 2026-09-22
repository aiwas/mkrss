using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Extraction;
using MkRss.Core.Sites;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Persistence;

public sealed class NpgsqlSiteRepositoryTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    /// <summary>更新日時の検証に使う固定の時刻。</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_IncludesDisabledSites() {
        // is_enabled は巡回の可否であり、管理画面には無効なサイトも並べる。
        await InsertSiteAsync("example-com-news-a1b2c3d4");
        await InsertSiteAsync("example-com-posts-e5f6a7b8");
        await ExecuteAsync(
            "update sites set is_enabled = false where slug = $1", "example-com-posts-e5f6a7b8");

        IReadOnlyList<SiteDefinition> sites = await CreateRepository().ListAsync(CancellationToken.None);

        Assert.Equal(2, sites.Count);
    }

    [Fact]
    public async Task ListAsync_MapsAllColumns() {
        await InsertSiteAsync();

        SiteDefinition site = Assert.Single(await CreateRepository().ListAsync(CancellationToken.None));

        Assert.Equal("example-com-news-a1b2c3d4", site.Draft.Slug);
        Assert.Equal("お知らせ一覧", site.Draft.Name);
        Assert.Equal("https://example.com/news/", site.Draft.PageUrl);
        Assert.True(site.Draft.IsEnabled);
        Assert.Equal(".news-item", site.Draft.Rule.ItemSelector);
        Assert.Equal(".news-title", site.Draft.Rule.Title.Selector);
        Assert.Null(site.Draft.Rule.Title.Attribute);
        Assert.Equal("a", site.Draft.Rule.Link.Selector);
        Assert.Equal("href", site.Draft.Rule.Link.Attribute);
        Assert.Equal(".news-date", site.Draft.Rule.Date?.Selector);
        Assert.Equal("datetime", site.Draft.Rule.Date?.Attribute);
        Assert.Equal(TimeSpan.Zero, site.Draft.Rule.DateOffset);
    }

    /// <summary>
    /// <c>date_selector</c> の <see langword="null"/> と空文字は別の意味を持つ。
    /// </summary>
    [Fact]
    public async Task ListAsync_DistinguishesNullAndBlankDateSelector() {
        await InsertSiteAsync("example-com-a-a1b2c3d4");
        await InsertSiteAsync("example-com-b-e5f6a7b8");
        await ExecuteAsync(
            "update sites set date_selector = null, date_attribute = null where slug = $1",
            "example-com-a-a1b2c3d4");
        await ExecuteAsync(
            "update sites set date_selector = '' where slug = $1", "example-com-b-e5f6a7b8");

        IReadOnlyList<SiteDefinition> sites = await CreateRepository().ListAsync(CancellationToken.None);

        Assert.Null(sites[0].Draft.Rule.Date);
        Assert.Equal(string.Empty, sites[1].Draft.Rule.Date?.Selector);
    }

    [Fact]
    public async Task FindAsync_ReturnsSiteWithMatchingId() {
        Guid id = await InsertSiteAsync();

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.Equal(id, site?.Id);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull() {
        await InsertSiteAsync();

        Assert.Null(await CreateRepository().FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_InsertsRowAndReturnsId() {
        SiteSaveResult result = await CreateRepository()
            .AddAsync(Draft(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            result.Id,
            await ScalarAsync<Guid>("select id from sites where slug = $1", "example-com-news-a1b2c3d4"));
    }

    [Fact]
    public async Task AddAsync_NoDateSelector_StoresNull() {
        SiteSaveResult result = await CreateRepository()
            .AddAsync(Draft() with { Rule = Rule() with { Date = null } }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(await ScalarAsync<bool>("select date_selector is null from sites"));
    }

    /// <summary>
    /// slug の重複は例外ではなく結果型で返す。
    /// </summary>
    /// <remarks>
    /// ランダム接尾辞の衝突と、利用者が既存の slug を入力した場合の両方で起きる。
    /// どちらも入力欄への指摘として示すべきものであり、異常終了させる理由がない。
    /// </remarks>
    [Fact]
    public async Task AddAsync_DuplicateSlug_ReturnsSlugTaken() {
        await InsertSiteAsync();

        SiteSaveResult result = await CreateRepository()
            .AddAsync(Draft(), CancellationToken.None);

        Assert.True(result.IsSlugTaken);
        Assert.Equal(1L, await ScalarAsync<long>("select count(*) from sites"));
    }

    [Fact]
    public async Task UpdateAsync_ChangesAllColumns() {
        Guid id = await InsertSiteAsync();
        SiteDraft changed = Draft("example-com-blog-99887766") with {
            Name = "ブログ",
            PageUrl = "https://example.com/blog/",
            IsEnabled = false,
            Rule = Rule() with {
                ItemSelector = "a.post",
                Link = new FieldSelector(string.Empty, "href"),
                Date = null,
                DateOffset = TimeSpan.FromHours(9),
            },
        };

        SiteSaveResult result = await CreateRepository().UpdateAsync(id, changed, CancellationToken.None);

        Assert.True(result.IsSuccess);

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.Equal(changed, site?.Draft);
    }

    /// <summary>
    /// <c>updated_at</c> を自動更新するトリガは置いていないため、アプリ側で書く。
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt() {
        Guid id = await InsertSiteAsync();

        await CreateRepository().UpdateAsync(id, Draft(), CancellationToken.None);

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.Equal(Now, site?.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_KeepsCreatedAt() {
        Guid id = await InsertSiteAsync();

        // timestamptz は Npgsql の既定では DateTime(UTC)として返る。
        DateTime createdAt = await ScalarAsync<DateTime>(
            "select created_at from sites where id = $1", id);

        await CreateRepository().UpdateAsync(id, Draft(), CancellationToken.None);

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.Equal(createdAt, site?.CreatedAt.UtcDateTime);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateSlug_ReturnsSlugTaken() {
        Guid id = await InsertSiteAsync("example-com-news-a1b2c3d4");
        await InsertSiteAsync("example-com-posts-e5f6a7b8");

        SiteSaveResult result = await CreateRepository()
            .UpdateAsync(id, Draft("example-com-posts-e5f6a7b8"), CancellationToken.None);

        Assert.True(result.IsSlugTaken);
        Assert.Equal(
            "example-com-news-a1b2c3d4",
            await ScalarAsync<string>("select slug from sites where id = $1", id));
    }

    [Fact]
    public async Task SetEnabledAsync_ChangesOnlyIsEnabled() {
        Guid id = await InsertSiteAsync();

        await CreateRepository().SetEnabledAsync(id, false, CancellationToken.None);

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.False(site?.Draft.IsEnabled);
        Assert.Equal("お知らせ一覧", site?.Draft.Name);
        Assert.Equal(".news-item", site?.Draft.Rule.ItemSelector);
    }

    [Fact]
    public async Task SetEnabledAsync_SetsUpdatedAt() {
        Guid id = await InsertSiteAsync();

        await CreateRepository().SetEnabledAsync(id, false, CancellationToken.None);

        SiteDefinition? site = await CreateRepository().FindAsync(id, CancellationToken.None);

        Assert.Equal(Now, site?.UpdatedAt);
    }

    private NpgsqlSiteRepository CreateRepository() {
        return new NpgsqlSiteRepository(Fixture.DataSource, new FixedTimeProvider(Now));
    }

    private static SiteDraft Draft(string slug = "example-com-news-a1b2c3d4") {
        return new SiteDraft(
            Slug: slug,
            Name: "お知らせ一覧",
            PageUrl: "https://example.com/news/",
            IsEnabled: true,
            Rule: Rule());
    }

    private static ExtractionRule Rule() {
        return new ExtractionRule(
            ItemSelector: ".news-item",
            Title: new FieldSelector(".news-title"),
            Link: new FieldSelector("a", "href"),
            Date: new FieldSelector(".news-date", "datetime"),
            DateOffset: TimeSpan.Zero);
    }
}
