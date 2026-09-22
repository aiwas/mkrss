using System;
using System.Collections.Generic;
using System.Text;
using MkRss.Core.Extraction;
using MkRss.Core.Tests.Fixtures;
using Xunit;

namespace MkRss.Core.Tests.Extraction;

public sealed class HtmlArticleExtractorTests {
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);
    private static readonly Uri PageUrl = new("https://example.com/news/");
    private static readonly DateTimeOffset FetchedAt = new(2026, 9, 21, 3, 0, 0, Jst);

    private static readonly ExtractionRule NewsListingRule = new(
        ItemSelector: "li.news-item",
        Title: new FieldSelector("h2.news-title"),
        Link: new FieldSelector("a", "href"),
        Date: new FieldSelector("time.news-date", "datetime"),
        DateOffset: Jst);

    [Fact]
    public void Extract_NewsListing_ReturnsArticlesInDocumentOrder() {
        string html = HtmlFixture.Load("news-listing.html");

        IReadOnlyList<ExtractedArticle> actual =
            HtmlArticleExtractor.Extract(html, PageUrl, NewsListingRule, FetchedAt);

        Assert.Equal(
            [
                new ExtractedArticle(
                    "最初のお知らせ",
                    "https://example.com/news/2026/09/20/first-post",
                    new DateTimeOffset(2026, 9, 20, 10, 30, 0, Jst)),
                new ExtractedArticle(
                    "二番目のお知らせ",
                    "https://example.com/news/2026/09/18/second-post?id=42",
                    new DateTimeOffset(2026, 9, 18, 9, 0, 0, Jst)),
                new ExtractedArticle(
                    "三番目のお知らせ",
                    "https://example.com/news/2026/09/15/third-post",
                    new DateTimeOffset(2026, 9, 15, 12, 0, 0, Jst)),
            ],
            actual);
    }

    [Fact]
    public void Extract_Utf8Bytes_ReturnsSameArticlesAsString() {
        byte[] content = HtmlFixture.LoadBytes("news-listing.html");

        IReadOnlyList<ExtractedArticle> actual = HtmlArticleExtractor.Extract(
            content, declaredCharset: null, PageUrl, NewsListingRule, FetchedAt);

        Assert.Equal(
            HtmlArticleExtractor.Extract(
                HtmlFixture.Load("news-listing.html"), PageUrl, NewsListingRule, FetchedAt),
            actual);
    }

    [Fact]
    public void Extract_ShiftJisDeclaredOnlyInMeta_IsDecodedCorrectly() {
        // HTTPヘッダには text/html としか書かず、<meta> でのみ宣言するサイトが存在する。
        // 復号前のバイト列を渡さないと、この宣言を読む機会そのものが無い。
        byte[] content = EncodeShiftJis(ShiftJisPage("Shift_JIS"));

        IReadOnlyList<ExtractedArticle> actual = HtmlArticleExtractor.Extract(
            content, declaredCharset: null, PageUrl, NewsListingRule, FetchedAt);

        Assert.Equal("最初のお知らせ", Assert.Single(actual).Title);
    }

    [Fact]
    public void Extract_DeclaredCharset_TakesPrecedenceOverMeta() {
        // HTML仕様では、転送層が申告した文字コードがページ内の宣言より優先される。
        // <meta> の宣言が誤っているサイトでも、ヘッダが正しければ読めることになる。
        byte[] content = EncodeShiftJis(ShiftJisPage("UTF-8"));

        IReadOnlyList<ExtractedArticle> actual = HtmlArticleExtractor.Extract(
            content, declaredCharset: "shift_jis", PageUrl, NewsListingRule, FetchedAt);

        Assert.Equal("最初のお知らせ", Assert.Single(actual).Title);
    }

    [Fact]
    public void Extract_UnresolvableDeclaredCharset_FallsBackToPageDeclaration() {
        // 申告された名前を解釈できないことを理由にページ全体を捨てない。
        byte[] content = EncodeShiftJis(ShiftJisPage("Shift_JIS"));

        IReadOnlyList<ExtractedArticle> actual = HtmlArticleExtractor.Extract(
            content, declaredCharset: "x-unknown-encoding", PageUrl, NewsListingRule, FetchedAt);

        Assert.Equal("最初のお知らせ", Assert.Single(actual).Title);
    }

    /// <summary>
    /// <paramref name="metaCharset"/> を宣言する一覧ページのHTMLを組み立てる。
    /// </summary>
    private static string ShiftJisPage(string metaCharset) {
        return $"""
            <!DOCTYPE html>
            <html lang="ja">
            <head><meta charset="{metaCharset}"></head>
            <body>
              <ul>
                <li class="news-item">
                  <h2 class="news-title">最初のお知らせ</h2>
                  <a href="/news/first">続きを読む</a>
                  <time class="news-date" datetime="2026-09-20T10:30:00+09:00">2026年9月20日</time>
                </li>
              </ul>
            </body>
            </html>
            """;
    }

    private static byte[] EncodeShiftJis(string html) {
        // 登録はプロセス全体に適用されるため、他のテストの実行順に結果が左右されないよう
        // ここで明示的に呼ぶ。本番は MkRss.Web.Program の合成ルートで登録している。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding("shift_jis").GetBytes(html);
    }

    [Fact]
    public void Extract_EmptyFieldSelector_TargetsItemElementItself() {
        string html = HtmlFixture.Load("anchor-items.html");
        ExtractionRule rule = new(
            ItemSelector: "a.post",
            Title: new FieldSelector("span.post-title"),
            Link: new FieldSelector("", "href"),
            Date: null,
            DateOffset: Jst);

        IReadOnlyList<ExtractedArticle> actual =
            HtmlArticleExtractor.Extract(html, PageUrl, rule, FetchedAt);

        Assert.Equal(
            [
                new ExtractedArticle("記事A", "https://example.com/p/1", FetchedAt),
                new ExtractedArticle("記事B", "https://example.com/p/2", FetchedAt),
            ],
            actual);
    }

    [Fact]
    public void Extract_IncompleteItems_SkipsThemAndFallsBackToFetchedAt() {
        string html = HtmlFixture.Load("incomplete-items.html");
        ExtractionRule rule = new(
            ItemSelector: "li.row",
            Title: new FieldSelector("h3.row-title"),
            Link: new FieldSelector("a", "href"),
            Date: new FieldSelector("time.row-date"),
            DateOffset: Jst);

        IReadOnlyList<ExtractedArticle> actual =
            HtmlArticleExtractor.Extract(html, PageUrl, rule, FetchedAt);

        // リンクが無い記事、スキームが不正な記事、タイトルが空白のみの記事は除外される。
        // 日付を解釈できない記事と日付要素が無い記事は、取得日時を使って残る。
        Assert.Equal(
            [
                new ExtractedArticle("日付が読めない記事", "https://example.com/unparsable-date", FetchedAt),
                new ExtractedArticle("日付要素の無い記事", "https://example.com/no-date", FetchedAt),
            ],
            actual);
    }

    [Fact]
    public void Extract_ItemSelectorMatchesNothing_ReturnsEmpty() {
        string html = HtmlFixture.Load("news-listing.html");
        ExtractionRule rule = NewsListingRule with { ItemSelector = "li.does-not-exist" };

        IReadOnlyList<ExtractedArticle> actual =
            HtmlArticleExtractor.Extract(html, PageUrl, rule, FetchedAt);

        Assert.Empty(actual);
    }
}
