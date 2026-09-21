using System;
using MkRss.Core.Feeds;
using Xunit;

namespace MkRss.Core.Tests.Feeds;

public sealed class FeedUrlResolverTests {
    private static readonly Uri RequestUrl = new("http://127.0.0.1:8080/feeds/example-a1b2c3d4.xml");

    private const string PathAndQuery = "/feeds/example-a1b2c3d4.xml";

    [Fact]
    public void Resolve_NoPublicBaseUrl_UsesRequestUrl() {
        Assert.Equal(RequestUrl, FeedUrlResolver.Resolve(null, RequestUrl, PathAndQuery));
    }

    [Fact]
    public void Resolve_EmptyPublicBaseUrl_UsesRequestUrl() {
        Assert.Equal(RequestUrl, FeedUrlResolver.Resolve("", RequestUrl, PathAndQuery));
    }

    [Fact]
    public void Resolve_PublicBaseUrl_ReplacesSchemeAndHost() {
        // プロキシの背後ではリクエストが http で届く。公開URLは https でなければならない。
        Uri resolved = FeedUrlResolver.Resolve(
            "https://feeds.erleis.dev", RequestUrl, PathAndQuery);

        Assert.Equal("https://feeds.erleis.dev/feeds/example-a1b2c3d4.xml", resolved.AbsoluteUri);
    }

    [Fact]
    public void Resolve_PublicBaseUrlWithTrailingSlash_DoesNotDuplicateSeparator() {
        Uri resolved = FeedUrlResolver.Resolve(
            "https://feeds.erleis.dev/", RequestUrl, PathAndQuery);

        Assert.Equal("https://feeds.erleis.dev/feeds/example-a1b2c3d4.xml", resolved.AbsoluteUri);
    }
}
