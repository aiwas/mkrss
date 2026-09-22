using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Feeds;
using Npgsql;

namespace MkRss.Infrastructure.Persistence;

/// <summary>
/// フィードの内容を PostgreSQL から読み出す <see cref="IFeedSource"/>。
/// </summary>
public sealed class NpgsqlFeedSource : IFeedSource {
    private const string SelectSiteSql = """
        select id, name, page_url from sites where slug = $1
        """;

    /// <summary>
    /// 配信する記事を新しい順に読み出す。
    /// </summary>
    /// <remarks>
    /// <c>published_at</c> だけでは順序が定まらない。日付のみの表記は0時に丸められ、
    /// 日付を抽出できない記事には同一クロールの取得時刻が入るため、同順位が頻繁に生じる。
    /// PostgreSQL は同順位の並びを保証しないため、応答ごとに順序が変わったり、
    /// <c>limit</c> の境界で記事が入れ替わったりする。
    /// ETag は配信するXMLのバイト列から計算しているため、順序が変われば ETag も変わる。
    /// 内容が実質同じでも毎回 200 を返すことになり、条件付きGETが機能しなくなる。
    /// <c>first_seen_at</c> はクロールをまたぐ同順位を解決し(後から見つけた記事を上にする)、
    /// <c>normalized_url</c> は同一クロール内の同順位で一意性を保証する。
    /// </remarks>
    private const string SelectEntriesSql = """
        select title, normalized_url, published_at
        from articles
        where site_id = $1
        order by published_at desc, first_seen_at desc, normalized_url
        limit $2
        """;

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlFeedSource(NpgsqlDataSource dataSource) {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<FeedData?> FindBySlugAsync(
        string slug,
        int limit,
        CancellationToken cancellationToken) {
        FeedSite? site = await FindSiteAsync(slug, cancellationToken);

        if (site is null) {
            return null;
        }

        return new FeedData(site, await ReadEntriesAsync(site.Id, limit, cancellationToken));
    }

    private async Task<FeedSite?> FindSiteAsync(string slug, CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SelectSiteSql);

        command.Parameters.Add(new NpgsqlParameter { Value = slug });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) {
            return null;
        }

        return new FeedSite(reader.GetGuid(0), reader.GetString(1), new Uri(reader.GetString(2)));
    }

    private async Task<IReadOnlyList<FeedEntry>> ReadEntriesAsync(
        Guid siteId,
        int limit,
        CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SelectEntriesSql);

        command.Parameters.Add(new NpgsqlParameter { Value = siteId });
        command.Parameters.Add(new NpgsqlParameter { Value = limit });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        List<FeedEntry> entries = [];

        while (await reader.ReadAsync(cancellationToken)) {
            entries.Add(new FeedEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2)));
        }

        return entries;
    }
}
