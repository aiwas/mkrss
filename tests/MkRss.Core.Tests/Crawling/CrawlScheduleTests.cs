using System;
using MkRss.Core.Crawling;
using Xunit;

namespace MkRss.Core.Tests.Crawling;

public sealed class CrawlScheduleTests {
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NextRunAt_NoPreviousRun_RunsImmediately() {
        CrawlSchedule schedule = new(Interval);

        Assert.Equal(Now, schedule.NextRunAt(lastStartedAt: null, Now));
    }

    [Fact]
    public void NextRunAt_WithinInterval_WaitsUntilIntervalElapses() {
        CrawlSchedule schedule = new(Interval);
        DateTimeOffset lastStartedAt = Now.AddMinutes(-20);

        Assert.Equal(Now.AddMinutes(40), schedule.NextRunAt(lastStartedAt, Now));
    }

    [Fact]
    public void NextRunAt_IntervalAlreadyElapsed_RunsImmediately() {
        // プロセスが止まっていた間の実行は、起動した時点で1回だけ取り戻す。
        // 過去の時刻を返すと、待ち時間が負になる。
        CrawlSchedule schedule = new(Interval);
        DateTimeOffset lastStartedAt = Now.AddHours(-5);

        Assert.Equal(Now, schedule.NextRunAt(lastStartedAt, Now));
    }

    [Fact]
    public void NextRunAt_LastStartedInFuture_WaitsNoLongerThanInterval() {
        // 開始時刻はDBの now() で記録され、現在時刻はアプリの時計から取る。
        // 両者がずれていても、間隔より長く待たないようにする。
        CrawlSchedule schedule = new(Interval);
        DateTimeOffset lastStartedAt = Now.AddMinutes(30);

        Assert.Equal(Now + Interval, schedule.NextRunAt(lastStartedAt, Now));
    }
}
