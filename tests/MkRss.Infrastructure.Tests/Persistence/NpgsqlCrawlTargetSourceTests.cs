using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using MkRss.Infrastructure.Persistence;
using MkRss.Infrastructure.Tests.Database;
using Xunit;

namespace MkRss.Infrastructure.Tests.Persistence;

public sealed class NpgsqlCrawlTargetSourceTests(PostgresFixture fixture) : DatabaseTestBase(fixture) {
    [Fact]
    public async Task ListEnabledAsync_ExcludesDisabledSites() {
        await InsertSiteAsync("example-com-news-a1b2c3d4");
        await InsertSiteAsync("example-com-posts-e5f6a7b8");
        await ExecuteAsync(
            "update sites set is_enabled = false where slug = $1", "example-com-posts-e5f6a7b8");

        IReadOnlyList<CrawlTarget> targets = await ListAsync();

        Assert.Equal("https://example.com/news/", Assert.Single(targets).PageUrl.AbsoluteUri);
    }

    [Fact]
    public async Task ListEnabledAsync_MapsExtractionRule() {
        await InsertSiteAsync();

        CrawlTarget target = Assert.Single(await ListAsync());

        Assert.Equal("お知らせ一覧", target.Name);
        Assert.Equal(".news-item", target.Rule.ItemSelector);
        Assert.Equal(".news-title", target.Rule.Title.Selector);
        Assert.Null(target.Rule.Title.Attribute);
        Assert.Equal("a", target.Rule.Link.Selector);
        Assert.Equal("href", target.Rule.Link.Attribute);
        Assert.Equal(".news-date", target.Rule.Date?.Selector);
        Assert.Equal("datetime", target.Rule.Date?.Attribute);
        Assert.Equal(TimeSpan.Zero, target.Rule.DateOffset);
    }

    [Fact]
    public async Task ListEnabledAsync_NullDateSelector_MapsToNoDateRule() {
        // date_selector が null の行は「日付を抽出しない」を意味する。
        await InsertSiteAsync();
        await ExecuteAsync("update sites set date_selector = null, date_attribute = null");

        Assert.Null(Assert.Single(await ListAsync()).Rule.Date);
    }

    [Fact]
    public async Task ListEnabledAsync_BlankSelector_IsPreserved() {
        // 空文字は「記事要素自身」を意味する。null と取り違えてはならない。
        await InsertSiteAsync();
        await ExecuteAsync("update sites set link_selector = '', date_selector = ''");

        CrawlTarget target = Assert.Single(await ListAsync());

        Assert.Equal(string.Empty, target.Rule.Link.Selector);
        Assert.Equal(string.Empty, target.Rule.Date?.Selector);
    }

    [Fact]
    public async Task ListEnabledAsync_MapsDateOffset() {
        await InsertSiteAsync();
        await ExecuteAsync("update sites set date_offset = interval '9 hours'");

        Assert.Equal(TimeSpan.FromHours(9), Assert.Single(await ListAsync()).Rule.DateOffset);
    }

    private async Task<IReadOnlyList<CrawlTarget>> ListAsync() {
        return await new NpgsqlCrawlTargetSource(Fixture.DataSource)
            .ListEnabledAsync(CancellationToken.None);
    }
}
