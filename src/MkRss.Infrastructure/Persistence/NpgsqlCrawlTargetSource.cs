using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using MkRss.Core.Extraction;
using Npgsql;

namespace MkRss.Infrastructure.Persistence;

/// <summary>
/// 巡回対象を PostgreSQL から読み出す <see cref="ICrawlTargetSource"/>。
/// </summary>
public sealed class NpgsqlCrawlTargetSource : ICrawlTargetSource {
    private const string SelectSql = """
        select id, name, page_url,
               item_selector,
               title_selector, title_attribute,
               link_selector, link_attribute,
               date_selector, date_attribute,
               date_offset
        from sites
        where is_enabled
        order by name
        """;

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlCrawlTargetSource(NpgsqlDataSource dataSource) {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrawlTarget>> ListEnabledAsync(CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SelectSql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        List<CrawlTarget> targets = [];

        while (await reader.ReadAsync(cancellationToken)) {
            targets.Add(ReadTarget(reader));
        }

        return targets;
    }

    private static CrawlTarget ReadTarget(NpgsqlDataReader reader) {
        ExtractionRule rule = new(
            ItemSelector: reader.GetString(3),
            Title: new FieldSelector(reader.GetString(4), ReadNullableString(reader, 5)),
            Link: new FieldSelector(reader.GetString(6), ReadNullableString(reader, 7)),
            Date: ReadDateSelector(reader),
            DateOffset: reader.GetFieldValue<TimeSpan>(10));

        return new CrawlTarget(
            Id: reader.GetGuid(0),
            Name: reader.GetString(1),
            PageUrl: new Uri(reader.GetString(2)),
            Rule: rule);
    }

    /// <summary>
    /// 日付の取得方法を読む。
    /// </summary>
    /// <remarks>
    /// <c>date_selector</c> が <c>null</c> の場合は日付を抽出しない。
    /// 空文字は「記事要素自身から取る」という別の意味になるため、取り違えてはならない。
    /// </remarks>
    private static FieldSelector? ReadDateSelector(NpgsqlDataReader reader) {
        string? selector = ReadNullableString(reader, 8);

        return selector is null ? null : new FieldSelector(selector, ReadNullableString(reader, 9));
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
