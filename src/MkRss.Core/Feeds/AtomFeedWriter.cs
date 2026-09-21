using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace MkRss.Core.Feeds;

/// <summary>
/// 記事一覧を Atom 1.0 フィードとして出力する。
/// </summary>
public static class AtomFeedWriter {
    /// <summary>
    /// Atom 1.0 形式のXML文字列を生成する。
    /// </summary>
    /// <param name="definition">フィード自身の情報。</param>
    /// <param name="entries">出力する記事。渡された順序がそのまま出力順になる。</param>
    /// <param name="updatedAt">フィードの最終更新日時。</param>
    public static string Write(
        FeedDefinition definition,
        IReadOnlyList<FeedEntry> entries,
        DateTimeOffset updatedAt) {
        SyndicationFeed feed = new() {
            Id = ToFeedId(definition.Id),
            Title = new TextSyndicationContent(definition.Title),
            LastUpdatedTime = updatedAt,
            Items = CreateItems(entries),
        };

        feed.Links.Add(SyndicationLink.CreateSelfLink(definition.FeedUrl));
        feed.Links.Add(SyndicationLink.CreateAlternateLink(definition.SiteUrl));

        // RFC 4287 は atom:feed に1つ以上の atom:author を要求する
        // (全ての atom:entry が author を持つ場合を除く)。
        // 記事ごとの著者は抽出していないため、巡回元のホスト名をフィード全体の著者として扱う。
        feed.Authors.Add(new SyndicationPerson { Name = definition.SiteUrl.Host });

        return Serialize(feed);
    }

    /// <summary>
    /// フィードの識別子を <c>atom:id</c> に書ける形へ変換する。
    /// </summary>
    /// <remarks>
    /// RFC 4287 は <c>atom:id</c> の値を IRI と定めており、裸のUUIDは該当しない。
    /// フィードURLを使わないのは、URLに含まれる slug が後から変更されうるため。
    /// <c>atom:id</c> は恒久的な値であることが期待される。
    /// </remarks>
    private static string ToFeedId(Guid id) {
        return "urn:uuid:" + id.ToString("D");
    }

    private static List<SyndicationItem> CreateItems(IReadOnlyList<FeedEntry> entries) {
        List<SyndicationItem> items = [];

        foreach (FeedEntry entry in entries) {
            Uri url = new(entry.Url);

            SyndicationItem item = new() {
                Id = entry.Url,
                Title = new TextSyndicationContent(entry.Title),
                PublishDate = entry.PublishedAt,
                LastUpdatedTime = entry.PublishedAt,
            };

            item.Links.Add(SyndicationLink.CreateAlternateLink(url));
            items.Add(item);
        }

        return items;
    }

    private static string Serialize(SyndicationFeed feed) {
        // XmlWriter は書き込み先の TextWriter.Encoding をXML宣言に書く。
        // StringWriter の既定は UTF-16 なので、UTF-8として配信すると宣言と実体が食い違う。
        using Utf8StringWriter output = new();

        XmlWriterSettings settings = new() {
            Indent = true,
        };

        using (var writer = XmlWriter.Create(output, settings)) {
            new Atom10FeedFormatter(feed).WriteTo(writer);
        }

        return output.ToString();
    }

    /// <summary>
    /// <see cref="Encoding.UTF8"/> を名乗る <see cref="StringWriter"/>。
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
