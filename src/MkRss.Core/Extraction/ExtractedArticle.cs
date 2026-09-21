using System;

namespace MkRss.Core.Extraction;

/// <summary>
/// ウェブページから抽出した記事1件。
/// </summary>
/// <param name="Title">記事タイトル。</param>
/// <param name="Url">正規化済みの記事URL。</param>
/// <param name="PublishedAt">
/// 記事の日付。抽出できなかった場合はページの取得日時が入る。
/// </param>
public sealed record ExtractedArticle(string Title, string Url, DateTimeOffset PublishedAt);
