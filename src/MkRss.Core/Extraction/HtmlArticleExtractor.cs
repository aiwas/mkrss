using System;
using System.Collections.Generic;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MkRss.Core.Dates;
using MkRss.Core.Urls;

namespace MkRss.Core.Extraction;

/// <summary>
/// HTMLから記事一覧を抽出する。
/// </summary>
public static class HtmlArticleExtractor {
    /// <summary>
    /// 取得したバイト列から <paramref name="rule"/> に従って記事を抽出する。
    /// </summary>
    /// <param name="content">取得したページのバイト列。</param>
    /// <param name="declaredCharset">
    /// HTTPの応答が申告した文字コード。申告が無かった場合は <see langword="null"/>。
    /// </param>
    /// <param name="pageUrl">相対URLの解決に使うページのURL。</param>
    /// <param name="rule">抽出規則。</param>
    /// <param name="fetchedAt">日付を抽出できなかった場合に使う取得日時。</param>
    /// <remarks>
    /// 復号はAngleSharpに任せる。BOM、申告された文字コード、ページ内の <c>&lt;meta charset&gt;</c>
    /// という順で判定される(HTML仕様の定める順序)。
    /// <c>&lt;meta&gt;</c> でのみ宣言するページを正しく読むには、復号前のバイト列を渡す必要がある。
    /// </remarks>
    public static IReadOnlyList<ExtractedArticle> Extract(
        ReadOnlyMemory<byte> content,
        string? declaredCharset,
        Uri pageUrl,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        using IHtmlDocument document =
            CreateParser().ParseDocument(content, ResolveEncoding(declaredCharset));

        return ExtractFrom(document, pageUrl, rule, fetchedAt);
    }

    /// <summary>
    /// 復号済みのHTMLから <paramref name="rule"/> に従って記事を抽出する。
    /// </summary>
    /// <param name="html">取得したページのHTML。</param>
    /// <param name="pageUrl">相対URLの解決に使うページのURL。</param>
    /// <param name="rule">抽出規則。</param>
    /// <param name="fetchedAt">日付を抽出できなかった場合に使う取得日時。</param>
    /// <remarks>
    /// 文字コードが確定している入力を対象とする。巡回で取得したページには使わない。
    /// </remarks>
    public static IReadOnlyList<ExtractedArticle> Extract(
        string html,
        Uri pageUrl,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        using IHtmlDocument document = CreateParser().ParseDocument(html);

        return ExtractFrom(document, pageUrl, rule, fetchedAt);
    }

    /// <summary>
    /// 申告された文字コードの名前を <see cref="Encoding"/> へ変換する。
    /// </summary>
    /// <remarks>
    /// 解釈できない名前は申告が無かったものとして扱い、ページ自身の宣言に任せる。
    /// 申告を理由にページ全体を捨てるより、判定の余地を残すほうが取りこぼしが少ない。
    /// </remarks>
    private static Encoding? ResolveEncoding(string? declaredCharset) {
        if (string.IsNullOrWhiteSpace(declaredCharset)) {
            return null;
        }

        try {
            return Encoding.GetEncoding(declaredCharset);
        } catch (ArgumentException) {
            return null;
        }
    }

    /// <remarks>
    /// HtmlParser を静的に共有するとスレッド安全性が保証されないため、呼び出しごとに生成する。
    /// 生成コスト自体は小さく、クロールはI/O待ちが支配的なので影響しない。
    /// </remarks>
    private static HtmlParser CreateParser() {
        return new HtmlParser();
    }

    /// <summary>
    /// 解析済みの文書から記事を取り出す。
    /// </summary>
    private static IReadOnlyList<ExtractedArticle> ExtractFrom(
        IHtmlDocument document,
        Uri pageUrl,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        List<ExtractedArticle> articles = [];

        foreach (IElement item in document.QuerySelectorAll(rule.ItemSelector)) {
            ExtractedArticle? article = ExtractOne(item, pageUrl, rule, fetchedAt);

            if (article is not null) {
                articles.Add(article);
            }
        }

        return articles;
    }

    /// <summary>
    /// 記事要素1件を <see cref="ExtractedArticle"/> へ変換する。
    /// タイトルまたはURLを取得できない場合は <see langword="null"/> を返す。
    /// </summary>
    private static ExtractedArticle? ExtractOne(
        IElement item,
        Uri pageUrl,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        string? title = ReadValue(item, rule.Title);

        if (string.IsNullOrEmpty(title)) {
            return null;
        }

        string? href = ReadValue(item, rule.Link);

        if (href is null) {
            return null;
        }

        string? url = UrlNormalizer.Normalize(pageUrl, href);

        if (url is null) {
            return null;
        }

        return new ExtractedArticle(title, url, ReadPublishedAt(item, rule, fetchedAt));
    }

    private static DateTimeOffset ReadPublishedAt(
        IElement item,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        if (rule.Date is null) {
            return fetchedAt;
        }

        string? text = ReadValue(item, rule.Date);

        if (text is null) {
            return fetchedAt;
        }

        return ArticleDateParser.Parse(text, rule.DateOffset) ?? fetchedAt;
    }

    /// <summary>
    /// <paramref name="selector"/> に従って値を取り出す。
    /// 要素または属性が存在しない場合は <see langword="null"/> を返す。
    /// </summary>
    private static string? ReadValue(IElement item, FieldSelector selector) {
        IElement? target = selector.Selector.Length == 0
            ? item
            : item.QuerySelector(selector.Selector);

        if (target is null) {
            return null;
        }

        string? value = selector.Attribute is null
            ? target.TextContent
            : target.GetAttribute(selector.Attribute);

        return value?.Trim();
    }
}
