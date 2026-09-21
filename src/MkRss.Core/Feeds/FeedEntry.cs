using System;

namespace MkRss.Core.Feeds;

/// <summary>
/// フィードに出力する記事1件。
/// </summary>
/// <param name="Title">記事タイトル。</param>
/// <param name="Url">正規化済みの記事URL。Atomの <c>id</c> と <c>link</c> の両方に使う。</param>
/// <param name="PublishedAt">記事の日付。</param>
public sealed record FeedEntry(string Title, string Url, DateTimeOffset PublishedAt);
