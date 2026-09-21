using System;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using Npgsql;

namespace MkRss.Infrastructure.Persistence;

/// <summary>
/// クロール実行の記録を PostgreSQL に残す <see cref="ICrawlRunLog"/>。
/// </summary>
public sealed class NpgsqlCrawlRunLog : ICrawlRunLog {
    private const string InsertSql = """
        insert into crawl_runs (status) values ('running') returning id
        """;

    private const string FinishSql = """
        update crawl_runs
        set status = $2, finished_at = now(), new_article_count = $3, error_message = $4
        where id = $1
        """;

    private const string LatestSql = """
        select started_at, finished_at, status, new_article_count, error_message
        from crawl_runs
        order by started_at desc
        limit 1
        """;

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlCrawlRunLog(NpgsqlDataSource dataSource) {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<Guid> StartAsync(CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(InsertSql);

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task FinishAsync(
        Guid runId,
        CrawlRunOutcome outcome,
        int newArticleCount,
        string? errorMessage,
        CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(FinishSql);

        command.Parameters.Add(new NpgsqlParameter { Value = runId });
        command.Parameters.Add(new NpgsqlParameter { Value = ToStatus(outcome) });
        command.Parameters.Add(new NpgsqlParameter { Value = newArticleCount });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)errorMessage ?? DBNull.Value });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrawlRunStatus?> FindLatestAsync(CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(LatestSql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) {
            return null;
        }

        return new CrawlRunStatus(
            StartedAt: reader.GetFieldValue<DateTimeOffset>(0),
            FinishedAt: reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            Status: reader.GetString(2),
            NewArticleCount: reader.GetInt32(3),
            ErrorMessage: reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    /// <summary>
    /// 結末を <c>crawl_runs.status</c> のCHECK制約が許す値へ変換する。
    /// </summary>
    private static string ToStatus(CrawlRunOutcome outcome) {
        return outcome switch {
            CrawlRunOutcome.Succeeded => "succeeded",
            CrawlRunOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
    }
}
