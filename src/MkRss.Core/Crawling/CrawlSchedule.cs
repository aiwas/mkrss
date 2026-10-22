using System;

namespace MkRss.Core.Crawling;

/// <summary>
/// 定期クロールの次の実行時刻を決める。
/// </summary>
/// <param name="Interval">実行の間隔。</param>
/// <remarks>
/// 基準は直近の実行の開始時刻(<c>crawl_runs.started_at</c>)。プロセスの中に持たず
/// DBの記録から求めるため、再起動をまたいでも間隔が保たれる。
/// </remarks>
public sealed record CrawlSchedule(TimeSpan Interval) {
    /// <summary>
    /// 次に実行する時刻を返す。現在時刻より前になることはない。
    /// </summary>
    /// <param name="lastStartedAt">直近の実行の開始時刻。記録が無い場合は <see langword="null"/>。</param>
    /// <param name="now">現在時刻。</param>
    public DateTimeOffset NextRunAt(DateTimeOffset? lastStartedAt, DateTimeOffset now) {
        if (lastStartedAt is null) {
            return now;
        }

        DateTimeOffset due = lastStartedAt.Value + Interval;

        // プロセスが止まっていた間に過ぎた実行は、何回分であっても1回だけ取り戻す。
        if (due < now) {
            return now;
        }

        // 開始時刻はDBの now() で記録され、現在時刻はアプリの時計から取る。
        // 両者がずれて開始時刻が未来になっていても、間隔より長くは待たない。
        DateTimeOffset latest = now + Interval;

        return due > latest ? latest : due;
    }
}
