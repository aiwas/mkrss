using System;
using MkRss.Core.Extraction;
using MkRss.Core.Sites;
using Xunit;

namespace MkRss.Core.Tests.Sites;

public sealed class SiteFormTests {
    private static readonly SiteDraft Draft = new(
        Slug: "example-com-news-a1b2c3d4",
        Name: "お知らせ一覧",
        PageUrl: "https://example.com/news/",
        IsEnabled: true,
        Rule: new ExtractionRule(
            ItemSelector: ".news-item",
            Title: new FieldSelector(".news-title"),
            Link: new FieldSelector("a", "href"),
            Date: new FieldSelector(".news-date", "datetime"),
            DateOffset: TimeSpan.FromHours(9)));

    [Fact]
    public void ToDraft_AfterFrom_ReturnsSameDraft() {
        SiteForm form = SiteForm.From(Draft);

        Assert.Equal(Draft, form.ToDraft());
    }

    /// <summary>
    /// 日付を抽出しない指定は <c>date_selector</c> を <see langword="null"/> にする。
    /// </summary>
    /// <remarks>
    /// 空文字は「記事要素自身から抽出する」という別の意味になる。
    /// 入力欄に文字が残っていても、チェックを外した時点で無視する。
    /// </remarks>
    [Fact]
    public void ToDraft_WithoutDate_ReturnsNullDateSelector() {
        SiteForm form = SiteForm.From(Draft);

        form.ExtractsDate = false;

        Assert.Null(form.ToDraft().Rule.Date);
    }

    [Fact]
    public void From_NullDateSelector_ClearsExtractsDate() {
        SiteForm form = SiteForm.From(Draft with { Rule = Draft.Rule with { Date = null } });

        Assert.False(form.ExtractsDate);
    }

    /// <summary>
    /// 空文字のセレクタは「記事要素自身から抽出する」指定であり、抽出しない指定ではない。
    /// </summary>
    [Fact]
    public void From_BlankDateSelector_KeepsExtractsDate() {
        SiteDraft draft = Draft with {
            Rule = Draft.Rule with { Date = new FieldSelector(string.Empty, "datetime") },
        };

        SiteForm form = SiteForm.From(draft);

        Assert.True(form.ExtractsDate);
        Assert.Equal(draft, form.ToDraft());
    }

    /// <summary>
    /// 属性欄が空のときは要素のテキストを読む指定にする。
    /// </summary>
    /// <remarks>
    /// HTMLの入力欄は未入力を空文字として返す。そのまま渡すと
    /// 「名前が空文字の属性を読む」という指定になり、値を取得できない。
    /// </remarks>
    [Fact]
    public void ToDraft_BlankAttribute_BecomesNull() {
        SiteForm form = SiteForm.From(Draft);

        form.TitleAttribute = string.Empty;
        form.DateAttribute = "   ";

        ExtractionRule rule = form.ToDraft().Rule;

        Assert.Null(rule.Title.Attribute);
        Assert.Null(rule.Date?.Attribute);
    }

    [Fact]
    public void ToDraft_DateOffsetHours_BecomesTimeSpan() {
        SiteForm form = SiteForm.From(Draft);

        form.DateOffsetHours = -5;

        Assert.Equal(TimeSpan.FromHours(-5), form.ToDraft().Rule.DateOffset);
    }
}
