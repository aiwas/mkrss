using System;
using MkRss.Core.Dates;
using Xunit;

namespace MkRss.Core.Tests.Dates;

public sealed class ArticleDateParserTests {
    /// <summary>オフセットを持たない入力に適用する既定オフセット(JST)。</summary>
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

    [Theory]
    [InlineData("2026-09-20T10:30:00+09:00")]
    [InlineData("2026-09-20T01:30:00Z")]
    [InlineData("2026-09-19T21:30:00-04:00")]
    public void Parse_WithExplicitOffset_UsesThatOffset(string text) {
        DateTimeOffset? actual = ArticleDateParser.Parse(text, Jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 30, 0, Jst), actual);
    }

    [Fact]
    public void Parse_DateTimeWithoutOffset_AppliesGivenOffset() {
        DateTimeOffset? actual = ArticleDateParser.Parse("2026-09-20T10:30:00", Jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 10, 30, 0, Jst), actual);
    }

    [Fact]
    public void Parse_DateOnly_IsMidnightAtGivenOffset() {
        DateTimeOffset? actual = ArticleDateParser.Parse("2026-09-20", Jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, Jst), actual);
    }

    [Fact]
    public void Parse_DateOnly_WithDifferentOffset_ShiftsInstant() {
        DateTimeOffset? actual = ArticleDateParser.Parse("2026-09-20", TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero), actual);
    }

    [Theory]
    [InlineData("2026/09/20")]
    [InlineData("2026/9/20")]
    [InlineData("2026年9月20日")]
    [InlineData("2026年09月20日")]
    public void Parse_LocalizedDateFormats_AreUnderstood(string text) {
        DateTimeOffset? actual = ArticleDateParser.Parse(text, Jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, Jst), actual);
    }

    [Theory]
    [InlineData("  2026-09-20  ")]
    [InlineData("\t2026-09-20\n")]
    public void Parse_SurroundingWhitespace_IsIgnored(string text) {
        DateTimeOffset? actual = ArticleDateParser.Parse(text, Jst);

        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, Jst), actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("公開日")]
    [InlineData("3日前")]
    [InlineData("昨日")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("20")]
    [InlineData("2026")]
    public void Parse_UnparsableText_ReturnsNull(string text) {
        DateTimeOffset? actual = ArticleDateParser.Parse(text, Jst);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("10:30")]
    [InlineData("10:30:45")]
    public void Parse_TimeWithoutDate_ReturnsNull(string text) {
        // 実行日が補われると再クロールのたびに日付が変わるため、解釈不能として扱う。
        DateTimeOffset? actual = ArticleDateParser.Parse(text, Jst);

        Assert.Null(actual);
    }

    [Fact]
    public void Parse_DateWithoutYear_AssumesCurrentYear() {
        DateTimeOffset? actual = ArticleDateParser.Parse("9/20", Jst);

        Assert.Equal(new DateTimeOffset(DateTime.Today.Year, 9, 20, 0, 0, 0, Jst), actual);
    }
}
