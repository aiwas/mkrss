using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 実行の終了と、サイトごとの内訳を1文で書き込む。
    /// </summary>
    /// <remarks>
    /// 更新をCTEに置くことで、合計と内訳が別々のトランザクションになる状態を避ける。
    /// データを変更するCTEは、主問い合わせがその結果を参照しなくても必ず実行されるため、
    /// 内訳が0件(<c>unnest</c> が0行)でも <c>crawl_runs</c> の更新は行われる。
    /// </remarks>
    private const string FinishSql = """
        with finished as (
            update crawl_runs
            set status = $2, finished_at = now(), new_article_count = $3, error_message = $4
            where id = $1
        )
        insert into crawl_run_sites (
            crawl_run_id, site_id, outcome, extracted_article_count, new_article_count, failure_reason)
        select $1, * from unnest($5::uuid[], $6::text[], $7::integer[], $8::integer[], $9::text[])
        """;

    private const string LatestSql = """
        select id, started_at, finished_at, status, new_article_count, error_message
        from crawl_runs
        order by started_at desc
        limit 1
        """;

    /// <summary>
    /// サイト名は <c>sites</c> から引く。記録時点の名前ではなく現在の名前になるが、
    /// 応答を読んで対象を判別するための表示であり、履歴として保持する値ではない。
    /// </summary>
    private const string SitesSql = """
        select r.site_id, s.name, r.outcome,
               r.extracted_article_count, r.new_article_count, r.failure_reason
        from crawl_run_sites r
        join sites s on s.id = r.site_id
        where r.crawl_run_id = $1
        order by s.name
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
        CrawlReport report,
        string? errorMessage,
        CancellationToken cancellationToken) {
        IReadOnlyList<SiteCrawlResult> sites = report.Sites;

        Guid[] siteIds = new Guid[sites.Count];
        string[] outcomes = new string[sites.Count];
        int[] extractedCounts = new int[sites.Count];
        int[] newCounts = new int[sites.Count];
        string?[] failureReasons = new string?[sites.Count];

        for (int index = 0; index < sites.Count; index++) {
            siteIds[index] = sites[index].SiteId;
            outcomes[index] = ToOutcome(sites[index].Outcome);
            extractedCounts[index] = sites[index].ExtractedArticleCount;
            newCounts[index] = sites[index].NewArticleCount;
            failureReasons[index] = sites[index].FailureReason;
        }

        await using NpgsqlCommand command = _dataSource.CreateCommand(FinishSql);

        command.Parameters.Add(new NpgsqlParameter { Value = runId });
        command.Parameters.Add(new NpgsqlParameter { Value = ToStatus(outcome) });
        command.Parameters.Add(new NpgsqlParameter { Value = report.NewArticleCount });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)errorMessage ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = siteIds });
        command.Parameters.Add(new NpgsqlParameter { Value = outcomes });
        command.Parameters.Add(new NpgsqlParameter { Value = extractedCounts });
        command.Parameters.Add(new NpgsqlParameter { Value = newCounts });
        command.Parameters.Add(new NpgsqlParameter { Value = failureReasons });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrawlRunStatus?> FindLatestAsync(CancellationToken cancellationToken) {
        (Guid RunId, CrawlRunStatus Status)? latest = await FindLatestRunAsync(cancellationToken);

        if (latest is null) {
            return null;
        }

        return latest.Value.Status with {
            Sites = await ReadSitesAsync(latest.Value.RunId, cancellationToken),
        };
    }

    /// <summary>
    /// 直近の実行を、内訳を除いて読み出す。
    /// </summary>
    private async Task<(Guid RunId, CrawlRunStatus Status)?> FindLatestRunAsync(
        CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(LatestSql);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) {
            return null;
        }

        return (
            reader.GetGuid(0),
            new CrawlRunStatus(
                StartedAt: reader.GetFieldValue<DateTimeOffset>(1),
                FinishedAt: reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                Status: reader.GetString(3),
                NewArticleCount: reader.GetInt32(4),
                ErrorMessage: reader.IsDBNull(5) ? null : reader.GetString(5),
                Sites: []));
    }

    /// <summary>
    /// 1回の実行におけるサイトごとの内訳を読み出す。
    /// </summary>
    private async Task<IReadOnlyList<CrawlRunSiteStatus>> ReadSitesAsync(
        Guid runId,
        CancellationToken cancellationToken) {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SitesSql);

        command.Parameters.Add(new NpgsqlParameter { Value = runId });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        List<CrawlRunSiteStatus> sites = [];

        while (await reader.ReadAsync(cancellationToken)) {
            sites.Add(new CrawlRunSiteStatus(
                SiteId: reader.GetGuid(0),
                SiteName: reader.GetString(1),
                Outcome: reader.GetString(2),
                ExtractedArticleCount: reader.GetInt32(3),
                NewArticleCount: reader.GetInt32(4),
                FailureReason: reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return sites;
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

    /// <summary>
    /// サイトごとの結末を <c>crawl_run_sites.outcome</c> のCHECK制約が許す値へ変換する。
    /// </summary>
    private static string ToOutcome(CrawlOutcome outcome) {
        return outcome switch {
            CrawlOutcome.Succeeded => "succeeded",
            CrawlOutcome.FetchFailed => "fetch_failed",
            CrawlOutcome.ExtractionFailed => "extraction_failed",
            CrawlOutcome.DisallowedByRobots => "disallowed_by_robots",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
    }
}
