using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MkRss.Core.Fetching;
using MkRss.Infrastructure.Fetching;
using Xunit;

namespace MkRss.Infrastructure.Tests.Fetching;

public sealed class HttpPageFetcherTests {
    private static readonly Uri PageUrl = new("https://example.com/news/");
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 3, 0, 0, TimeSpan.FromHours(9));

    private static readonly PageFetcherOptions Options = new(
        UserAgent: "mkrss/1.0 (+https://example.net/about)",
        Timeout: TimeSpan.FromSeconds(30));

    /// <summary>常に同じ時刻を返す <see cref="TimeProvider"/>。</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static HttpPageFetcher CreateFetcher(
        StubHttpMessageHandler handler,
        PageFetcherOptions? options = null) {
        return new HttpPageFetcher(
            new HttpClient(handler),
            options ?? Options,
            new FixedTimeProvider(Now),
            NullLogger<HttpPageFetcher>.Instance);
    }

    private static HttpResponseMessage Html(string body) {
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(body, Encoding.UTF8, "text/html"),
        };
    }

    [Fact]
    public async Task FetchAsync_SuccessfulResponse_ReturnsHtmlAndFetchedAt() {
        StubHttpMessageHandler handler = new(Html("<html><body>本文</body></html>"));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Equal("<html><body>本文</body></html>", actual.Html);
        Assert.Equal(Now, actual.FetchedAt);
    }

    [Fact]
    public async Task FetchAsync_SendsConfiguredUserAgent() {
        StubHttpMessageHandler handler = new(Html("<html></html>"));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(Options.UserAgent, request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task FetchAsync_RequestsTheGivenUrl() {
        StubHttpMessageHandler handler = new(Html("<html></html>"));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(PageUrl, Assert.Single(handler.Requests).RequestUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task FetchAsync_UnsuccessfulStatusCode_ReturnsFailure(HttpStatusCode status) {
        StubHttpMessageHandler handler = new(new HttpResponseMessage(status));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Null(actual.Html);
        Assert.Contains(((int)status).ToString(), actual.FailureReason);
        Assert.Equal(Now, actual.FetchedAt);
    }

    [Fact]
    public async Task FetchAsync_ShiftJisCharsetInContentType_IsDecoded() {
        // 日本語サイトでは Shift_JIS が現役で使われている。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding shiftJis = Encoding.GetEncoding("shift_jis");

        HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(shiftJis.GetBytes("<html><body>日本語</body></html>")),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html") {
            CharSet = "shift_jis",
        };

        HttpPageFetcher fetcher = CreateFetcher(new StubHttpMessageHandler(response));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal("<html><body>日本語</body></html>", actual.Html);
    }

    [Fact]
    public async Task FetchAsync_NetworkError_ReturnsFailureInsteadOfThrowing() {
        StubHttpMessageHandler handler = new(
            (_, _) => throw new HttpRequestException("名前解決に失敗しました"));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Contains("名前解決に失敗しました", actual.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_ExceedingTimeout_ReturnsFailure() {
        PageFetcherOptions options = Options with { Timeout = TimeSpan.FromMilliseconds(50) };
        StubHttpMessageHandler handler = new(async (_, token) => {
            await Task.Delay(TimeSpan.FromSeconds(10), token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        HttpPageFetcher fetcher = CreateFetcher(handler, options);

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Contains("タイムアウト", actual.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_CallerCancellation_IsNotSwallowed() {
        // 呼び出し側によるキャンセルは失敗ではなく、そのまま伝播させる。
        using CancellationTokenSource cancellation = new();
        StubHttpMessageHandler handler = new(async (_, token) => {
            await cancellation.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        HttpPageFetcher fetcher = CreateFetcher(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fetcher.FetchAsync(PageUrl, cancellation.Token));
    }
}
