using System;
using MkRss.Core.Sites;
using Xunit;

namespace MkRss.Core.Tests.Sites;

public sealed class SlugGeneratorTests {
    /// <summary>テストで固定するランダム接尾辞。</summary>
    private const string Suffix = "a1b2c3d4";

    /// <summary>
    /// <c>sites_slug_format_check</c> の定める形式。
    /// マイグレーション <c>20260920145838_create_sites.sql</c> の CHECK 制約と同じ。
    /// </summary>
    private const string SlugFormat = "^[a-z0-9]+(-[a-z0-9]+)*$";

    [Fact]
    public void Generate_HostAndPath_AreJoinedWithSuffix() {
        var actual = SlugGenerator.Generate(new Uri("https://example.com/news/"), Suffix);

        Assert.Equal("example-com-news-a1b2c3d4", actual);
    }

    [Fact]
    public void Generate_UpperCasePath_IsLowerCased() {
        var actual = SlugGenerator.Generate(new Uri("https://example.com/News/"), Suffix);

        Assert.Equal("example-com-news-a1b2c3d4", actual);
    }

    [Theory]
    [InlineData("https://example.com/news/list.html", "example-com-news-list-html-a1b2c3d4")]
    [InlineData("https://example.com/news_archive/", "example-com-news-archive-a1b2c3d4")]
    [InlineData("https://example.com/a--b/", "example-com-a-b-a1b2c3d4")]
    [InlineData("https://example.com/-news-/", "example-com-news-a1b2c3d4")]
    public void Generate_NonAlphanumeric_BecomesSingleSeparator(string pageUrl, string expected) {
        var actual = SlugGenerator.Generate(new Uri(pageUrl), Suffix);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://example.com")]
    public void Generate_NoPath_UsesHostAndSuffixOnly(string pageUrl) {
        var actual = SlugGenerator.Generate(new Uri(pageUrl), Suffix);

        Assert.Equal("example-com-a1b2c3d4", actual);
    }

    /// <summary>
    /// 非ASCIIのホスト名は punycode を経由して組み立てる。
    /// </summary>
    /// <remarks>
    /// Unicodeのまま組み立てると、すべての文字が区切りに落ちてホスト名が消える。
    /// punycode の接頭辞 <c>xn--</c> はハイフンが連続するため、そのままでは
    /// <c>sites_slug_format_check</c> に適合しない。区切りの正規化によって
    /// <c>xn-</c> になる。punycode として復号できる形ではなくなるが、
    /// slug は人が読むための名前であり、後から編集できる値なので差し支えない。
    /// </remarks>
    [Fact]
    public void Generate_NonAsciiHost_IsPunycode() {
        var actual = SlugGenerator.Generate(new Uri("https://日本.example/"), Suffix);

        Assert.Equal("xn-wgv71a-example-a1b2c3d4", actual);
    }

    /// <summary>
    /// 非ASCIIを含むパスのセグメントは捨てる。
    /// </summary>
    /// <remarks>
    /// パーセントエンコードされた文字列をそのまま区切り処理に通すと、
    /// 16進の断片(<c>e3-83-8b</c> など)が slug に残る。
    /// </remarks>
    [Fact]
    public void Generate_PercentEncodedSegment_IsDropped() {
        var actual = SlugGenerator.Generate(new Uri("https://example.com/ニュース/"), Suffix);

        Assert.Equal("example-com-a1b2c3d4", actual);
    }

    [Fact]
    public void Generate_PercentEncodedSegment_DoesNotDropOtherSegments() {
        var actual = SlugGenerator.Generate(new Uri("https://example.com/ニュース/list/"), Suffix);

        Assert.Equal("example-com-list-a1b2c3d4", actual);
    }

    /// <summary>
    /// 生成した値は <c>sites_slug_format_check</c> に適合する。
    /// </summary>
    /// <remarks>
    /// 適合しない値を作ると、登録がDBのCHECK制約違反として失敗する。
    /// 個別の期待値を確かめるテストとは別に、形式そのものを固定しておく。
    /// </remarks>
    [Theory]
    [InlineData("https://example.com/news/")]
    [InlineData("https://example.com")]
    [InlineData("https://日本.example/ニュース/")]
    [InlineData("https://example.com/-news-/")]
    [InlineData("https://example.com/a--b/")]
    [InlineData("https://example.com/news/list.html?page=2")]
    public void Generate_Always_MatchesSlugFormat(string pageUrl) {
        var actual = SlugGenerator.Generate(new Uri(pageUrl), Suffix);

        Assert.Matches(SlugFormat, actual);
    }

    [Fact]
    public void NewSuffix_IsEightLowerCaseHexCharacters() {
        var actual = SlugGenerator.NewSuffix();

        Assert.Matches("^[0-9a-f]{8}$", actual);
    }

    [Fact]
    public void NewSuffix_DiffersBetweenCalls() {
        Assert.NotEqual(SlugGenerator.NewSuffix(), SlugGenerator.NewSuffix());
    }
}
