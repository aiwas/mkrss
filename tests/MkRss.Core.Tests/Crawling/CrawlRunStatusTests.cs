using System;
using MkRss.Core.Crawling;
using Xunit;

namespace MkRss.Core.Tests.Crawling;

public sealed class CrawlRunStatusTests {
    [Fact]
    public void ExtractedNothing_SucceededWithoutArticles_ReturnsTrue() {
        CrawlRunSiteStatus site = Site("succeeded", extracted: 0);

        Assert.True(site.ExtractedNothing);
    }

    [Fact]
    public void ExtractedNothing_SucceededWithArticles_ReturnsFalse() {
        // 新規が0件でも、抽出できていれば更新が無かっただけで正常。
        CrawlRunSiteStatus site = Site("succeeded", extracted: 3);

        Assert.False(site.ExtractedNothing);
    }

    [Fact]
    public void NeedsReview_SucceededWithSiteExtractingNothing_ReturnsTrue() {
        CrawlRunStatus run = Run("succeeded", Site("succeeded", extracted: 3), Site("succeeded", extracted: 0));

        Assert.True(run.NeedsReview);
    }

    [Fact]
    public void NeedsReview_AllSitesExtracted_ReturnsFalse() {
        CrawlRunStatus run = Run("succeeded", Site("succeeded", extracted: 3), Site("succeeded", extracted: 1));

        Assert.False(run.NeedsReview);
    }

    [Fact]
    public void NeedsReview_Failed_ReturnsFalse() {
        // 失敗のほうが重い。抽出0件のサイトを含んでいても「失敗あり」として表示する。
        CrawlRunStatus run = Run("failed", Site("fetch_failed", extracted: 0), Site("succeeded", extracted: 0));

        Assert.False(run.NeedsReview);
    }

    [Theory]
    [InlineData("fetch_failed")]
    [InlineData("extraction_failed")]
    [InlineData("disallowed_by_robots")]
    public void ExtractedNothing_NotSucceeded_ReturnsFalse(string outcome) {
        // 抽出まで到達していないサイトは、それぞれの結末として表示される。
        CrawlRunSiteStatus site = Site(outcome, extracted: 0);

        Assert.False(site.ExtractedNothing);
    }

    private static CrawlRunStatus Run(string status, params CrawlRunSiteStatus[] sites) {
        DateTimeOffset startedAt = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        return new CrawlRunStatus(Guid.NewGuid(), startedAt, startedAt.AddMinutes(1), status, 0, null, sites);
    }

    private static CrawlRunSiteStatus Site(string outcome, int extracted) {
        return new CrawlRunSiteStatus(Guid.NewGuid(), "サイト", outcome, extracted, 0, null);
    }
}
