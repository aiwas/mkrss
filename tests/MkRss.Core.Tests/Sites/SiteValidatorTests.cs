using System;
using MkRss.Core.Extraction;
using MkRss.Core.Sites;
using Xunit;

namespace MkRss.Core.Tests.Sites;

public sealed class SiteValidatorTests {
    /// <summary>
    /// 検証を通る入力。各テストはこれを1箇所だけ壊して確かめる。
    /// </summary>
    private static readonly SiteDraft Valid = new(
        Slug: "example-com-news-a1b2c3d4",
        Name: "Example ニュース",
        PageUrl: "https://example.com/news/",
        IsEnabled: true,
        Rule: new ExtractionRule(
            ItemSelector: ".news-item",
            Title: new FieldSelector(".news-title"),
            Link: new FieldSelector("a", "href"),
            Date: new FieldSelector(".news-date", "datetime"),
            DateOffset: TimeSpan.Zero));

    [Fact]
    public void Validate_ValidDraft_ReturnsNoError() {
        var actual = SiteValidator.Validate(Valid);

        Assert.Empty(actual);
    }

    /// <summary>
    /// <c>slug</c> は <c>sites_slug_format_check</c> に適合する必要がある。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Example")]
    [InlineData("example_com")]
    [InlineData("-example")]
    [InlineData("example-")]
    [InlineData("example--com")]
    [InlineData("example com")]
    public void Validate_MalformedSlug_ReturnsError(string slug) {
        var actual = SiteValidator.Validate(Valid with { Slug = slug });

        Assert.Equal(SiteField.Slug, Assert.Single(actual).Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_ReturnsError(string name) {
        var actual = SiteValidator.Validate(Valid with { Name = name });

        Assert.Equal(SiteField.Name, Assert.Single(actual).Field);
    }

    /// <summary>
    /// <c>page_url</c> は http または https の絶対URLである必要がある。
    /// </summary>
    /// <remarks>
    /// 相対URLを登録すると、巡回時ではなく取得時に失敗する形でしか現れない。
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/news/")]
    [InlineData("example.com/news/")]
    [InlineData("ftp://example.com/news/")]
    [InlineData("javascript:alert(1)")]
    public void Validate_PageUrlIsNotHttpAbsoluteUrl_ReturnsError(string pageUrl) {
        var actual = SiteValidator.Validate(Valid with { PageUrl = pageUrl });

        Assert.Equal(SiteField.PageUrl, Assert.Single(actual).Field);
    }

    /// <summary>
    /// <c>item_selector</c> の空文字は「記事要素自身」という解釈が成り立たない。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankItemSelector_ReturnsError(string itemSelector) {
        var actual = SiteValidator.Validate(WithRule(Valid.Rule with { ItemSelector = itemSelector }));

        Assert.Equal(SiteField.ItemSelector, Assert.Single(actual).Field);
    }

    /// <summary>
    /// CSSとして解釈できないセレクタは、対応する入力欄の問題として指摘する。
    /// </summary>
    /// <remarks>
    /// ここで弾かないと、巡回時に「そのサイトの抽出結果が0件」あるいは
    /// <c>CrawlOutcome.ExtractionFailed</c> という形でしか現れない。
    /// </remarks>
    [Fact]
    public void Validate_MalformedItemSelector_ReturnsError() {
        var actual = SiteValidator.Validate(WithRule(Valid.Rule with { ItemSelector = ".news[" }));

        Assert.Equal(SiteField.ItemSelector, Assert.Single(actual).Field);
    }

    [Fact]
    public void Validate_MalformedTitleSelector_ReturnsError() {
        var actual = SiteValidator.Validate(
            WithRule(Valid.Rule with { Title = new FieldSelector(".title[") }));

        Assert.Equal(SiteField.TitleSelector, Assert.Single(actual).Field);
    }

    [Fact]
    public void Validate_MalformedLinkSelector_ReturnsError() {
        var actual = SiteValidator.Validate(
            WithRule(Valid.Rule with { Link = new FieldSelector("a[", "href") }));

        Assert.Equal(SiteField.LinkSelector, Assert.Single(actual).Field);
    }

    [Fact]
    public void Validate_MalformedDateSelector_ReturnsError() {
        var actual = SiteValidator.Validate(
            WithRule(Valid.Rule with { Date = new FieldSelector(".date[", "datetime") }));

        Assert.Equal(SiteField.DateSelector, Assert.Single(actual).Field);
    }

    /// <summary>
    /// 空文字のセレクタは「記事要素自身を対象とする」という指定であり、問題ではない。
    /// </summary>
    /// <remarks>
    /// CSSとしては解釈できない文字列なので、構文の検証から除く必要がある。
    /// </remarks>
    [Fact]
    public void Validate_BlankFieldSelector_IsAccepted() {
        var actual = SiteValidator.Validate(WithRule(Valid.Rule with {
            Title = new FieldSelector(string.Empty),
            Link = new FieldSelector(string.Empty, "href"),
            Date = new FieldSelector(string.Empty, "datetime"),
        }));

        Assert.Empty(actual);
    }

    /// <summary>
    /// 日付を抽出しない指定(<c>date_selector</c> が null)は問題ではない。
    /// </summary>
    [Fact]
    public void Validate_NoDateSelector_IsAccepted() {
        var actual = SiteValidator.Validate(WithRule(Valid.Rule with { Date = null }));

        Assert.Empty(actual);
    }

    [Fact]
    public void Validate_SeveralProblems_ReturnsAllOfThem() {
        var actual = SiteValidator.Validate(Valid with { Slug = "Example", Name = "  " });

        Assert.Equal(2, actual.Count);
    }

    private static SiteDraft WithRule(ExtractionRule rule) {
        return Valid with { Rule = rule };
    }
}
