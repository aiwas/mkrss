using MkRss.Core.Robots;
using Xunit;

namespace MkRss.Core.Tests.Robots;

public sealed class RobotsTxtTests {
    private const string UserAgent = "mkrss";

    [Fact]
    public void IsAllowed_DisallowedPrefix_IsRejected() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow: /private
            """);

        Assert.False(robots.IsAllowed("/private", UserAgent));
        Assert.False(robots.IsAllowed("/private/page.html", UserAgent));
        Assert.True(robots.IsAllowed("/public", UserAgent));
    }

    [Fact]
    public void IsAllowed_EmptyDisallowValue_AllowsEverything() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow:
            """);

        Assert.True(robots.IsAllowed("/anything", UserAgent));
    }

    [Fact]
    public void IsAllowed_DisallowRoot_RejectsEverything() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow: /
            """);

        Assert.False(robots.IsAllowed("/", UserAgent));
        Assert.False(robots.IsAllowed("/news/", UserAgent));
    }

    private const string GroupedRobots = """
        User-agent: BadBot
        Disallow: /

        User-agent: *
        Disallow: /admin

        User-agent: mkrss
        Disallow: /internal
        """;

    [Fact]
    public void IsAllowed_ExactUserAgentGroup_TakesPrecedenceOverWildcard() {
        RobotsTxt robots = RobotsTxt.Parse(GroupedRobots);

        // mkrss 用のグループがあるため、`*` のグループは参照しない。
        Assert.False(robots.IsAllowed("/internal/page", "mkrss"));
        Assert.True(robots.IsAllowed("/admin/page", "mkrss"));
    }

    [Fact]
    public void IsAllowed_NoExactGroup_FallsBackToWildcard() {
        RobotsTxt robots = RobotsTxt.Parse(GroupedRobots);

        Assert.False(robots.IsAllowed("/admin/page", "other-crawler"));
        Assert.True(robots.IsAllowed("/internal/page", "other-crawler"));
    }

    [Fact]
    public void IsAllowed_UserAgentMatching_IsCaseInsensitive() {
        RobotsTxt robots = RobotsTxt.Parse(GroupedRobots);

        Assert.False(robots.IsAllowed("/internal/page", "MkRss"));
    }

    [Fact]
    public void IsAllowed_GroupWithSeveralUserAgents_AppliesToAllOfThem() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: alpha
            User-agent: beta
            Disallow: /shared
            """);

        Assert.False(robots.IsAllowed("/shared", "alpha"));
        Assert.False(robots.IsAllowed("/shared", "beta"));
        Assert.True(robots.IsAllowed("/shared", "gamma"));
    }

    [Fact]
    public void IsAllowed_NoMatchingGroup_AllowsEverything() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: SomeOtherBot
            Disallow: /
            """);

        Assert.True(robots.IsAllowed("/anything", UserAgent));
    }

    [Fact]
    public void IsAllowed_LongerAllowRule_OverridesDisallow() {
        // `Disallow: /` + `Allow: /news/` は頻出する。最長一致を採らないと全て拒否になる。
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow: /
            Allow: /news/
            """);

        Assert.True(robots.IsAllowed("/news/article", UserAgent));
        Assert.False(robots.IsAllowed("/private/page", UserAgent));
    }

    [Fact]
    public void IsAllowed_SameLengthAllowAndDisallow_PrefersAllow() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow: /news
            Allow: /news
            """);

        Assert.True(robots.IsAllowed("/news/article", UserAgent));
    }

    [Fact]
    public void IsAllowed_ShorterAllowRule_DoesNotOverrideDisallow() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Allow: /news
            Disallow: /news/secret
            """);

        Assert.True(robots.IsAllowed("/news/public", UserAgent));
        Assert.False(robots.IsAllowed("/news/secret/page", UserAgent));
    }

    [Fact]
    public void IsAllowed_PathComparison_IsCaseSensitive() {
        RobotsTxt robots = RobotsTxt.Parse("""
            User-agent: *
            Disallow: /Private
            """);

        Assert.False(robots.IsAllowed("/Private/page", UserAgent));
        Assert.True(robots.IsAllowed("/private/page", UserAgent));
    }

    [Fact]
    public void Parse_Comments_AreIgnored() {
        RobotsTxt robots = RobotsTxt.Parse("""
            # これはコメント
            User-agent: *   # 全てのクローラ
            Disallow: /admin  # 管理画面
            """);

        Assert.False(robots.IsAllowed("/admin/page", UserAgent));
        Assert.True(robots.IsAllowed("/news/page", UserAgent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    [InlineData("# コメントのみ")]
    [InlineData("意味のない行\nもう一行")]
    public void IsAllowed_EmptyOrMeaninglessContent_AllowsEverything(string content) {
        RobotsTxt robots = RobotsTxt.Parse(content);

        Assert.True(robots.IsAllowed("/anything", UserAgent));
    }

    [Fact]
    public void Parse_CarriageReturnLineEndings_AreHandled() {
        RobotsTxt robots = RobotsTxt.Parse("User-agent: *\r\nDisallow: /admin\r\n");

        Assert.False(robots.IsAllowed("/admin/page", UserAgent));
    }
}
