using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MkRss.Core.Feeds;
using Xunit;

namespace MkRss.Core.Tests.Feeds;

public sealed class AtomFeedWriterTests {
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

    private static readonly Guid FeedId = new("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static readonly FeedDefinition Definition = new(
        Id: FeedId,
        Title: "お知らせ一覧",
        FeedUrl: new Uri("https://mkrss.example.net/feeds/example-com-news-a1b2c3d4.xml"),
        SiteUrl: new Uri("https://example.com/news/"));

    private static readonly IReadOnlyList<FeedEntry> Entries = [
        new FeedEntry(
            "最初のお知らせ",
            "https://example.com/news/first",
            new DateTimeOffset(2026, 9, 20, 10, 30, 0, Jst)),
        new FeedEntry(
            "二番目のお知らせ",
            "https://example.com/news/second",
            new DateTimeOffset(2026, 9, 18, 9, 0, 0, Jst)),
    ];

    private static XElement WriteAndParse(
        FeedDefinition definition,
        IReadOnlyList<FeedEntry> entries,
        DateTimeOffset updatedAt) {
        string xml = AtomFeedWriter.Write(definition, entries, updatedAt);

        return XDocument.Parse(xml).Root!;
    }

    [Fact]
    public void Write_RootElement_IsAtomFeed() {
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        Assert.Equal(Atom + "feed", feed.Name);
    }

    [Fact]
    public void Write_FeedMetadata_IsWritten() {
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        Assert.Equal("お知らせ一覧", (string?)feed.Element(Atom + "title"));
    }

    [Fact]
    public void Write_FeedId_IsUrnUuid() {
        // RFC 4287: atom:id の値は IRI でなければならない。
        // 裸のUUIDは IRI ではないため、RFC 4122 の urn:uuid 形式で出力する。
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        Assert.Equal(
            "urn:uuid:0f8fad5b-d9cb-469f-a165-70867728950e",
            (string?)feed.Element(Atom + "id"));
    }

    [Fact]
    public void Write_FeedId_DoesNotDependOnFeedUrl() {
        // フィードURLに含まれる slug は管理UIから変更できる。
        // atom:id は恒久的な値であることが期待されるため、slugの変更に追随してはならない。
        FeedDefinition renamed = Definition with {
            FeedUrl = new Uri("https://mkrss.example.net/feeds/renamed-e5f6a7b8.xml"),
        };

        XElement before = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);
        XElement after = WriteAndParse(renamed, Entries, Entries[0].PublishedAt);

        Assert.Equal(
            (string?)before.Element(Atom + "id"),
            (string?)after.Element(Atom + "id"));
    }

    [Fact]
    public void Write_FeedLinks_ContainSelfAndAlternate() {
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        Dictionary<string, string?> links = feed.Elements(Atom + "link")
            .ToDictionary(
                link => (string)link.Attribute("rel")!,
                link => (string?)link.Attribute("href"));

        Assert.Equal(Definition.FeedUrl.AbsoluteUri, links["self"]);
        Assert.Equal(Definition.SiteUrl.AbsoluteUri, links["alternate"]);
    }

    [Fact]
    public void Write_Entries_PreserveOrderAndCarryIdAndLink() {
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        XElement[] entries = [.. feed.Elements(Atom + "entry")];

        Assert.Equal(2, entries.Length);
        Assert.Equal(
            ["最初のお知らせ", "二番目のお知らせ"],
            entries.Select(entry => (string?)entry.Element(Atom + "title")));
        Assert.Equal(
            ["https://example.com/news/first", "https://example.com/news/second"],
            entries.Select(entry => (string?)entry.Element(Atom + "id")));
        Assert.Equal(
            ["https://example.com/news/first", "https://example.com/news/second"],
            entries.Select(entry => (string?)entry.Element(Atom + "link")!.Attribute("href")));
    }

    [Fact]
    public void Write_EntryDate_IsRfc3339AndKeepsTheInstant() {
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        string published = (string)feed.Elements(Atom + "entry").First().Element(Atom + "published")!;

        Assert.Equal(Entries[0].PublishedAt, DateTimeOffset.Parse(published));
    }

    [Fact]
    public void Write_NoEntries_StillProducesValidFeed() {
        DateTimeOffset updatedAt = new(2026, 9, 21, 3, 0, 0, Jst);

        XElement feed = WriteAndParse(Definition, [], updatedAt);

        Assert.Equal(Atom + "feed", feed.Name);
        Assert.Empty(feed.Elements(Atom + "entry"));
        Assert.Equal(updatedAt, DateTimeOffset.Parse((string)feed.Element(Atom + "updated")!));
    }

    [Fact]
    public void Write_XmlDeclaration_DeclaresUtf8() {
        // UTF-8として配信するため、宣言も utf-8 でなければならない。
        string xml = AtomFeedWriter.Write(Definition, Entries, Entries[0].PublishedAt);

        Assert.Equal("utf-8", XDocument.Parse(xml).Declaration?.Encoding);
    }

    [Fact]
    public void Write_Feed_HasAuthor() {
        // RFC 4287: atom:feed は1つ以上の atom:author を含まなければならない
        // (全ての atom:entry が author を持つ場合を除く)。
        XElement feed = WriteAndParse(Definition, Entries, Entries[0].PublishedAt);

        XElement? author = feed.Element(Atom + "author");

        Assert.NotNull(author);
        Assert.Equal("example.com", (string?)author.Element(Atom + "name"));
    }

    [Fact]
    public void Write_SpecialCharactersInTitle_AreEscaped() {
        FeedEntry entry = new(
            "<b>&amp; \"引用\"</b>",
            "https://example.com/news/escaped",
            new DateTimeOffset(2026, 9, 20, 0, 0, 0, Jst));

        XElement feed = WriteAndParse(Definition, [entry], entry.PublishedAt);

        Assert.Equal(
            "<b>&amp; \"引用\"</b>",
            (string?)feed.Elements(Atom + "entry").First().Element(Atom + "title"));
    }
}
