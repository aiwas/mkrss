using System;
using MkRss.Core.Urls;
using Xunit;

namespace MkRss.Core.Tests.Urls;

public sealed class UrlNormalizerTests {
    private static readonly Uri BaseUri = new("https://example.com/news/");

    [Fact]
    public void Normalize_AbsoluteUrl_ReturnsSameUrl() {
        var actual = UrlNormalizer.Normalize(BaseUri, "https://example.com/news/article-1");

        Assert.Equal("https://example.com/news/article-1", actual);
    }

    [Theory]
    [InlineData("article-2", "https://example.com/news/article-2")]
    [InlineData("./article-2", "https://example.com/news/article-2")]
    [InlineData("../about", "https://example.com/about")]
    [InlineData("/other/article-3", "https://example.com/other/article-3")]
    [InlineData("//cdn.example.com/a", "https://cdn.example.com/a")]
    public void Normalize_RelativeUrl_IsResolvedAgainstBaseUri(string href, string expected) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_UpperCaseSchemeAndHost_AreLowerCased() {
        var actual = UrlNormalizer.Normalize(BaseUri, "HTTPS://EXAMPLE.COM/News/Article");

        Assert.Equal("https://example.com/News/Article", actual);
    }

    [Theory]
    [InlineData("https://example.com:443/news/a", "https://example.com/news/a")]
    [InlineData("http://example.com:80/news/a", "http://example.com/news/a")]
    public void Normalize_DefaultPort_IsRemoved(string href, string expected) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_NonDefaultPort_IsKept() {
        var actual = UrlNormalizer.Normalize(BaseUri, "https://example.com:8443/news/a");

        Assert.Equal("https://example.com:8443/news/a", actual);
    }

    [Fact]
    public void Normalize_Fragment_IsRemoved() {
        var actual = UrlNormalizer.Normalize(BaseUri, "https://example.com/news/a#section");

        Assert.Equal("https://example.com/news/a", actual);
    }

    [Theory]
    [InlineData("https://example.com/news/a?utm_source=twitter")]
    [InlineData("https://example.com/news/a?utm_source=x&utm_medium=social&utm_campaign=c")]
    [InlineData("https://example.com/news/a?UTM_SOURCE=x")]
    [InlineData("https://example.com/news/a?fbclid=abc")]
    [InlineData("https://example.com/news/a?gclid=abc")]
    public void Normalize_TrackingParameters_AreRemoved(string href) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Equal("https://example.com/news/a", actual);
    }

    [Fact]
    public void Normalize_NonTrackingParameters_AreKept() {
        var actual = UrlNormalizer.Normalize(BaseUri, "https://example.com/news/a?id=5&utm_source=x");

        Assert.Equal("https://example.com/news/a?id=5", actual);
    }

    [Fact]
    public void Normalize_RemainingParameters_AreSortedByName() {
        var actual = UrlNormalizer.Normalize(BaseUri, "https://example.com/news/a?b=2&a=1&c=3");

        Assert.Equal("https://example.com/news/a?a=1&b=2&c=3", actual);
    }

    [Theory]
    [InlineData("https://example.com/news/a?")]
    [InlineData("https://example.com/news/a?utm_source=x")]
    public void Normalize_EmptyResultingQuery_DropsQuestionMark(string href) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Equal("https://example.com/news/a", actual);
    }

    [Theory]
    [InlineData("https://example.com/news/a/", "https://example.com/news/a/")]
    [InlineData("https://example.com/news/a", "https://example.com/news/a")]
    public void Normalize_TrailingSlash_IsPreserved(string href, string expected) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:void(0)")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+81312345678")]
    [InlineData("http://")]
    public void Normalize_UnusableHref_ReturnsNull(string href) {
        var actual = UrlNormalizer.Normalize(BaseUri, href);

        Assert.Null(actual);
    }
}
