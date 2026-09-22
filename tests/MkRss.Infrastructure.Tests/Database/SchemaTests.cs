using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MkRss.Infrastructure.Tests.Database;

/// <summary>
/// <c>supabase/migrations/</c> のSQLが作るスキーマの検証。
/// </summary>
/// <remarks>
/// アプリケーションのコードではなくスキーマそのものを対象にしている。
/// CHECK制約やユニーク制約は「書いたつもりで効いていない」状態になっても
/// 通常のテストでは気づけないため、ここで固定する。
/// </remarks>
public sealed class SchemaTests : DatabaseTestBase {
    private const string InsertSite = """
        insert into sites (
            slug, name, page_url,
            item_selector, title_selector, link_selector, link_attribute,
            date_selector, date_attribute)
        values ($1, 'お知らせ一覧', 'https://example.com/news/', $2, '.news-title', 'a', 'href', $3, $4)
        """;

    public SchemaTests(PostgresFixture fixture) : base(fixture) {
    }

    [Fact]
    public async Task Migrations_AreApplied() {
        long tables = await ScalarAsync<long>(
            """
            select count(*) from information_schema.tables
            where table_schema = 'public'
              and table_name in ('sites', 'articles', 'crawl_runs', 'crawl_run_sites')
            """);

        Assert.Equal(4, tables);
    }

    [Theory]
    [InlineData("Example-Com")]
    [InlineData("example_com")]
    [InlineData("example--com")]
    [InlineData("-example")]
    [InlineData("example-")]
    [InlineData("example.com")]
    public async Task Slug_NotUsableInUrlPath_IsRejected(string slug) {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSiteAsync(slug));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("sites_slug_format_check", exception.ConstraintName);
    }

    [Fact]
    public async Task Slug_Duplicated_IsRejected() {
        await InsertSiteAsync("example-com-news-a1b2c3d4");

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSiteAsync("example-com-news-a1b2c3d4"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task ItemSelector_Blank_IsRejected() {
        // 空文字は「記事要素自身」を意味するが、item_selector ではその解釈が成り立たない。
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSiteAsync("example-com-blank-a1b2c3d4", itemSelector: "   "));

        Assert.Equal("sites_item_selector_not_blank", exception.ConstraintName);
    }

    [Fact]
    public async Task DateAttribute_WithoutDateSelector_IsRejected() {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSiteAsync(
                "example-com-orphan-a1b2c3d4",
                dateSelector: null,
                dateAttribute: "datetime"));

        Assert.Equal("sites_date_pair_check", exception.ConstraintName);
    }

    [Fact]
    public async Task DateSelector_Blank_IsAccepted() {
        // 空文字は記事要素自身から日付を取ることを意味する。null (日付を取らない) とは別物。
        await InsertSiteAsync(
            "example-com-self-a1b2c3d4",
            dateSelector: "",
            dateAttribute: "data-published");

        Assert.Equal(1, await CountSitesAsync());
    }

    [Fact]
    public async Task InsertArticles_OnConflictDoNothing_ReturnsOnlyNewOnes() {
        Guid siteId = await InsertSiteAsync("example-com-news-a1b2c3d4");

        await InsertArticlesAsync(siteId, "https://example.com/news/first", "https://example.com/news/second");

        long inserted = await ScalarAsync<long>(
            """
            with upserted as (
                insert into articles (site_id, normalized_url, title, published_at)
                select $1, unnest($2::text[]), '記事', now()
                on conflict (site_id, normalized_url) do nothing
                returning id)
            select count(*) from upserted
            """,
            siteId,
            new[] {
                "https://example.com/news/first",
                "https://example.com/news/second",
                "https://example.com/news/third",
            });

        Assert.Equal(1, inserted);
    }

    [Fact]
    public async Task SameUrl_OnDifferentSites_IsAccepted() {
        // 同じ記事URLが複数サイトの一覧に載ることがある。サイトごとに別のフィードを配信するため、
        // それぞれに記事として存在してよい。
        Guid first = await InsertSiteAsync("example-com-news-a1b2c3d4");
        Guid second = await InsertSiteAsync("example-com-posts-e5f6a7b8");

        await InsertArticlesAsync(first, "https://example.com/news/first");
        await InsertArticlesAsync(second, "https://example.com/news/first");

        Assert.Equal(2, await ScalarAsync<long>("select count(*) from articles"));
    }

    [Fact]
    public async Task DeleteSite_CascadesToArticles() {
        Guid siteId = await InsertSiteAsync("example-com-news-a1b2c3d4");
        await InsertArticlesAsync(siteId, "https://example.com/news/first");

        await ExecuteAsync("delete from sites where id = $1", siteId);

        Assert.Equal(0, await ScalarAsync<long>("select count(*) from articles"));
    }

    [Fact]
    public async Task CrawlRuns_UndefinedStatus_IsRejected() {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync("insert into crawl_runs (status) values ('unknown')"));

        Assert.Equal("crawl_runs_status_check", exception.ConstraintName);
    }

    [Fact]
    public async Task CrawlRunSites_UndefinedOutcome_IsRejected() {
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRunSiteAsync(runId, siteId, "unknown"));

        Assert.Equal("crawl_run_sites_outcome_check", exception.ConstraintName);
    }

    [Fact]
    public async Task CrawlRunSites_FailureReasonOnSuccess_IsRejected() {
        // 成功した行に失敗理由が入っているのは矛盾している。
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRunSiteAsync(runId, siteId, "succeeded", failureReason: "503"));

        Assert.Equal("crawl_run_sites_failure_reason_check", exception.ConstraintName);
    }

    [Fact]
    public async Task CrawlRunSites_FailureWithoutReason_IsAccepted() {
        // 理由を取得できない失敗を想定し、逆向きは強制しない。
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();

        await InsertRunSiteAsync(runId, siteId, "fetch_failed");

        Assert.Equal(1, await ScalarAsync<long>("select count(*) from crawl_run_sites"));
    }

    [Fact]
    public async Task CrawlRunSites_SameSiteTwiceInOneRun_IsRejected() {
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();

        await InsertRunSiteAsync(runId, siteId, "succeeded");

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRunSiteAsync(runId, siteId, "succeeded"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("crawl_run_sites_run_site_unique", exception.ConstraintName);
    }

    [Fact]
    public async Task DeleteCrawlRun_CascadesToCrawlRunSites() {
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();
        await InsertRunSiteAsync(runId, siteId, "succeeded");

        await ExecuteAsync("delete from crawl_runs where id = $1", runId);

        Assert.Equal(0, await ScalarAsync<long>("select count(*) from crawl_run_sites"));
    }

    [Fact]
    public async Task DeleteSite_CascadesToCrawlRunSites() {
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();
        await InsertRunSiteAsync(runId, siteId, "succeeded");

        await ExecuteAsync("delete from sites where id = $1", siteId);

        Assert.Equal(0, await ScalarAsync<long>("select count(*) from crawl_run_sites"));
    }

    [Fact]
    public async Task Reset_EmptiesEveryTable() {
        (Guid runId, Guid siteId) = await InsertRunAndSiteAsync();
        await InsertArticlesAsync(siteId, "https://example.com/news/first");
        await InsertRunSiteAsync(runId, siteId, "succeeded");

        await Fixture.ResetAsync();

        Assert.Equal(0, await CountSitesAsync());
        Assert.Equal(0, await ScalarAsync<long>("select count(*) from articles"));
        Assert.Equal(0, await ScalarAsync<long>("select count(*) from crawl_runs"));
        Assert.Equal(0, await ScalarAsync<long>("select count(*) from crawl_run_sites"));
    }

    /// <summary>
    /// <c>crawl_run_sites</c> の行が参照できる実行とサイトを1件ずつ用意する。
    /// </summary>
    private async Task<(Guid RunId, Guid SiteId)> InsertRunAndSiteAsync() {
        await ExecuteAsync("insert into crawl_runs (status) values ('running')");

        return (
            await ScalarAsync<Guid>("select id from crawl_runs"),
            await InsertSiteAsync("example-com-news-a1b2c3d4"));
    }

    private async Task InsertRunSiteAsync(
        Guid runId,
        Guid siteId,
        string outcome,
        string? failureReason = null) {
        await ExecuteAsync(
            """
            insert into crawl_run_sites (crawl_run_id, site_id, outcome, failure_reason)
            values ($1, $2, $3, $4)
            """,
            runId,
            siteId,
            outcome,
            failureReason);
    }

    private async Task<Guid> InsertSiteAsync(
        string slug,
        string itemSelector = ".news-item",
        string? dateSelector = ".news-date",
        string? dateAttribute = "datetime") {
        await ExecuteAsync(InsertSite, slug, itemSelector, dateSelector, dateAttribute);

        return await ScalarAsync<Guid>("select id from sites where slug = $1", slug);
    }

    private async Task InsertArticlesAsync(Guid siteId, params string[] urls) {
        await ExecuteAsync(
            """
            insert into articles (site_id, normalized_url, title, published_at)
            select $1, unnest($2::text[]), '記事', now()
            """,
            siteId,
            urls);
    }

    private async Task<long> CountSitesAsync() {
        return await ScalarAsync<long>("select count(*) from sites");
    }
}
