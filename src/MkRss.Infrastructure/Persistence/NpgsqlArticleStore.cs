using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using MkRss.Core.Extraction;
using Npgsql;

namespace MkRss.Infrastructure.Persistence;

/// <summary>
/// 記事を PostgreSQL に保存する <see cref="IArticleStore"/>。
/// </summary>
public sealed class NpgsqlArticleStore : IArticleStore {
    /// <summary>
    /// 配列をまとめて渡し、未登録のものだけを1往復で追加する。
    /// </summary>
    /// <remarks>
    /// <c>ON CONFLICT DO NOTHING</c> のため、既存の記事が含まれていても失敗しない。
    /// 追加された件数は更新行数として返る。
    /// </remarks>
    private const string InsertSql = """
        insert into articles (site_id, normalized_url, title, published_at)
        select $1, * from unnest($2::text[], $3::text[], $4::timestamptz[])
        on conflict (site_id, normalized_url) do nothing
        """;

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlArticleStore(NpgsqlDataSource dataSource) {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<int> SaveNewAsync(
        Guid siteId,
        IReadOnlyList<ExtractedArticle> articles,
        CancellationToken cancellationToken) {
        if (articles.Count == 0) {
            return 0;
        }

        string[] urls = new string[articles.Count];
        string[] titles = new string[articles.Count];
        DateTime[] publishedAt = new DateTime[articles.Count];

        for (int index = 0; index < articles.Count; index++) {
            urls[index] = articles[index].Url;
            titles[index] = articles[index].Title;

            // timestamptz へ書き込めるのは UTC の DateTime のみ。
            // 抽出時のオフセットはここで落とし、時刻そのものは保つ。
            publishedAt[index] = articles[index].PublishedAt.UtcDateTime;
        }

        await using NpgsqlCommand command = _dataSource.CreateCommand(InsertSql);

        command.Parameters.Add(new NpgsqlParameter { Value = siteId });
        command.Parameters.Add(new NpgsqlParameter { Value = urls });
        command.Parameters.Add(new NpgsqlParameter { Value = titles });
        command.Parameters.Add(new NpgsqlParameter { Value = publishedAt });

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
