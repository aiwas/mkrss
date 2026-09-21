using System;
using System.Collections.Generic;
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
    /// <paramref name="html"/> から <paramref name="rule"/> に従って記事を抽出する。
    /// </summary>
    /// <param name="html">取得したページのHTML。</param>
    /// <param name="pageUrl">相対URLの解決に使うページのURL。</param>
    /// <param name="rule">抽出規則。</param>
    /// <param name="fetchedAt">日付を抽出できなかった場合に使う取得日時。</param>
    public static IReadOnlyList<ExtractedArticle> Extract(
        string html,
        Uri pageUrl,
        ExtractionRule rule,
        DateTimeOffset fetchedAt) {
        // HtmlParser を静的に共有するとスレッド安全性が保証されないため、呼び出しごとに生成する。
        // 生成コスト自体は小さく、クロールはI/O待ちが支配的なので影響しない。
        HtmlParser parser = new();

        // IHtmlDocument は IDisposable。解放しないと内部のコンテキストが残る。
        using IHtmlDocument document = parser.ParseDocument(html);
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
