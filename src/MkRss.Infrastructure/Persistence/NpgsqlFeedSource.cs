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

    private const string SelectEntriesSql = """
        select title, normalized_url, published_at
        from articles
        where site_id = $1
        order by published_at desc
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
