using System;
using MkRss.Core.Extraction;

namespace MkRss.Core.Crawling;

/// <summary>
/// 1回のクロールで処理する巡回対象。
/// </summary>
/// <param name="Id">サイトの識別子。保存時に記事の所属先として使う。</param>
/// <param name="Name">サイト名。ログに出力する。</param>
/// <param name="PageUrl">巡回する一覧ページのURL。</param>
/// <param name="Rule">このページから記事を抽出する規則。</param>
public sealed record CrawlTarget(Guid Id, string Name, Uri PageUrl, ExtractionRule Rule);
