using System;

namespace MkRss.Core.Feeds;

/// <summary>
/// 1つのフィードを識別するための情報。
/// </summary>
/// <param name="Id">
/// このフィードの恒久的な識別子。Atomの <c>id</c> に使う。
/// </param>
/// <param name="Title">フィードのタイトル。通常は登録したサイト名。</param>
/// <param name="FeedUrl">
/// このフィード自身のURL。Atomの <c>rel="self"</c> のリンクに使う。
/// </param>
/// <param name="SiteUrl">
/// 巡回元のページURL。Atomの <c>rel="alternate"</c> のリンクに使う。
/// </param>
public sealed record FeedDefinition(Guid Id, string Title, Uri FeedUrl, Uri SiteUrl);
