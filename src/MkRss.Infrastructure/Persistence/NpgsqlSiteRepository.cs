using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Extraction;
using MkRss.Core.Sites;
using Npgsql;

namespace MkRss.Infrastructure.Persistence;

/// <summary>
/// サイトを PostgreSQL に読み書きする <see cref="ISiteRepository"/>。
/// </summary>
public sealed class NpgsqlSiteRepository : ISiteRepository {
    /// <summary>一意制約違反を表す PostgreSQL のエラーコード。</summary>
    private const string UniqueViolation = "23505";

    private const string SelectColumns = """
        SELECT
            id,
            slug,
            name,
            page_url,
            is_enabled,
            item_selector,
            title_selector,
            title_attribute,
            link_selector,
            link_attribute,
            date_selector,
            date_attribute,
            date_offset,
            created_at,
            updated_at
        FROM
            sites
        """;

    /// <remarks>
    /// 並び順の第2キーに <c>slug</c> を置く。<c>name</c> は同じ値を持てるため、
    /// これだけでは一覧の並びが実行ごとに変わりうる。
    /// </remarks>
    private const string ListSql = $"""
        {SelectColumns}
        ORDER BY name, slug
        """;

    private const string FindSql = $"""
        {SelectColumns}
        WHERE id = $1
        """;

    private const string InsertSql = """
        INSERT INTO sites
        (
            slug,
            name,
            page_url,
            is_enabled,
            item_selector,
            title_selector,
            title_attribute,
            link_selector,
            link_attribute,
            date_selector,
            date_attribute,
            date_offset,
            created_at,
            updated_at
        )
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $13)
        RETURNING id
        """;

    /// <remarks>
    /// <c>created_at</c> は書き換えない。<c>updated_at</c> を自動更新するトリガは
    /// 置いていないため、ここで明示的に現在時刻を入れる。
    /// </remarks>
    private const string UpdateSql = """
        UPDATE sites SET
            slug            = $1,
            name            = $2,
            page_url        = $3,
            is_enabled      = $4,
            item_selector   = $5,
            title_selector  = $6,
            title_attribute = $7,
            link_selector   = $8,
            link_attribute  = $9,
            date_selector   = $10,
            date_attribute  = $11,
            date_offset     = $12,
            updated_at      = $13
        WHERE id = $14
        """;

    private const string SetEnabledSql = """
        UPDATE sites SET
            is_enabled = $1,
            updated_at = $2
        WHERE id = $3
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;

    public NpgsqlSiteRepository(NpgsqlDataSource dataSource, TimeProvider timeProvider) {
        _dataSource = dataSource;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SiteDefinition>> ListAsync(CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(ListSql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        List<SiteDefinition> sites = [];

        while (await reader.ReadAsync(cancellationToken)) {
            sites.Add(ReadSite(reader));
        }

        return sites;
    }

    /// <inheritdoc />
    public async Task<SiteDefinition?> FindAsync(Guid id, CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(FindSql);

        command.Parameters.Add(new NpgsqlParameter { Value = id });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadSite(reader) : null;
    }

    /// <inheritdoc />
    public async Task<SiteSaveResult> AddAsync(SiteDraft draft, CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(InsertSql);

        AddDraftParameters(command, draft);

        // 作成と更新の日時を同じ値にする。行の時刻をすべて同じ時計から取る。
        command.Parameters.Add(new NpgsqlParameter { Value = _timeProvider.GetUtcNow().UtcDateTime });

        try {
            return SiteSaveResult.Saved((Guid)(await command.ExecuteScalarAsync(cancellationToken))!);
        } catch (PostgresException exception) when (exception.SqlState == UniqueViolation) {
            return SiteSaveResult.SlugTaken();
        }
    }

    /// <inheritdoc />
    public async Task<SiteSaveResult> UpdateAsync(
        Guid id,
        SiteDraft draft,
        CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(UpdateSql);

        AddDraftParameters(command, draft);
        Add(command, _timeProvider.GetUtcNow().UtcDateTime);
        Add(command, id);

        try {
            await command.ExecuteNonQueryAsync(cancellationToken);

            return SiteSaveResult.Saved(id);
        } catch (PostgresException exception) when (exception.SqlState == UniqueViolation) {
            return SiteSaveResult.SlugTaken();
        }
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SetEnabledSql);

        Add(command, isEnabled);
        Add(command, _timeProvider.GetUtcNow().UtcDateTime);
        Add(command, id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// <see cref="SiteDraft"/> の各列を <c>$1</c> から順に追加する。
    /// </summary>
    private static void AddDraftParameters(NpgsqlCommand command, SiteDraft draft) {
        ExtractionRule rule = draft.Rule;

        Add(command, draft.Slug);
        Add(command, draft.Name);
        Add(command, draft.PageUrl);
        Add(command, draft.IsEnabled);
        Add(command, rule.ItemSelector);
        Add(command, rule.Title.Selector);
        Add(command, rule.Title.Attribute);
        Add(command, rule.Link.Selector);
        Add(command, rule.Link.Attribute);

        // 日付を抽出しない場合はセレクタも属性も null。
        // 空文字は「記事要素自身から抽出する」という別の意味になる。
        Add(command, rule.Date?.Selector);
        Add(command, rule.Date?.Attribute);

        Add(command, rule.DateOffset);
    }

    private static void Add(NpgsqlCommand command, object? value) {
        command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
    }

    private static SiteDefinition ReadSite(NpgsqlDataReader reader) {
        ExtractionRule rule = new(
            ItemSelector: reader.GetString(5),
            Title: new FieldSelector(reader.GetString(6), ReadNullableString(reader, 7)),
            Link: new FieldSelector(reader.GetString(8), ReadNullableString(reader, 9)),
            Date: ReadDateSelector(reader),
            DateOffset: reader.GetFieldValue<TimeSpan>(12));

        SiteDraft draft = new(
            Slug: reader.GetString(1),
            Name: reader.GetString(2),
            PageUrl: reader.GetString(3),
            IsEnabled: reader.GetBoolean(4),
            Rule: rule);

        return new SiteDefinition(
            Id: reader.GetGuid(0),
            Draft: draft,
            CreatedAt: reader.GetFieldValue<DateTimeOffset>(13),
            UpdatedAt: reader.GetFieldValue<DateTimeOffset>(14));
    }

    /// <summary>
    /// 日付の取得方法を読む。
    /// </summary>
    /// <remarks>
    /// <c>date_selector</c> が <c>null</c> の場合は日付を抽出しない。
    /// 空文字は「記事要素自身から取る」という別の意味になるため、取り違えてはならない。
    /// </remarks>
    private static FieldSelector? ReadDateSelector(NpgsqlDataReader reader) {
        string? selector = ReadNullableString(reader, 10);

        return selector is null ? null : new FieldSelector(selector, ReadNullableString(reader, 11));
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
