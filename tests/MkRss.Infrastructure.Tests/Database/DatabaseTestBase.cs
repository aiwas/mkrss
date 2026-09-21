using System;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace MkRss.Infrastructure.Tests.Database;

/// <summary>
/// DBを使うテストの基底クラス。テストごとにテーブルを空にする。
/// </summary>
/// <remarks>
/// 既定ではスキップされる。実行するには環境変数
/// <c>MKRSS_INTEGRATION_TESTS=1</c> を設定する。
/// </remarks>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Integration")]
public abstract class DatabaseTestBase : IAsyncLifetime {
    protected DatabaseTestBase(PostgresFixture fixture) {
        Fixture = fixture;
    }

    protected PostgresFixture Fixture { get; }

    public virtual async ValueTask InitializeAsync() {
        Assert.SkipUnless(IntegrationTestEnvironment.IsEnabled, IntegrationTestEnvironment.SkipReason);

        await Fixture.ResetAsync();
    }

    public virtual ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// SQLを実行する。<paramref name="values"/> はSQL中の <c>$1</c> から順に対応する。
    /// </summary>
    protected async Task ExecuteAsync(string sql, params object?[] values) {
        await using NpgsqlCommand command = CreateCommand(sql, values);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 単一の値を返すSQLを実行する。
    /// </summary>
    protected async Task<T> ScalarAsync<T>(string sql, params object?[] values) {
        await using NpgsqlCommand command = CreateCommand(sql, values);

        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// 記事の保存先として使えるサイトを1件登録し、その識別子を返す。
    /// </summary>
    protected async Task<Guid> InsertSiteAsync(string slug = "example-com-news-a1b2c3d4") {
        await ExecuteAsync(
            """
            insert into sites (
                slug, name, page_url,
                item_selector, title_selector, link_selector, link_attribute,
                date_selector, date_attribute)
            values ($1, 'お知らせ一覧', 'https://example.com/news/',
                '.news-item', '.news-title', 'a', 'href', '.news-date', 'datetime')
            """,
            slug);

        return await ScalarAsync<Guid>("select id from sites where slug = $1", slug);
    }

    private NpgsqlCommand CreateCommand(string sql, object?[] values) {
        NpgsqlCommand command = Fixture.DataSource.CreateCommand(sql);

        foreach (object? value in values) {
            command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        }

        return command;
    }
}
