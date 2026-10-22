using System;
using System.IO;
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

    /// <summary>テストで使う取得サイズの上限。本番の既定値より小さくしてある。</summary>
    private const int MaxPageBytes = 1024;

    private static readonly PageFetcherOptions Options = new(
        UserAgent: "mkrss/1.0 (+https://example.net/about)",
        Timeout: TimeSpan.FromSeconds(30),
        MaxPageBytes: MaxPageBytes);

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

    /// <summary>
    /// 任意のバイト列を、指定した <paramref name="charset"/> を申告して返す応答を作る。
    /// </summary>
    private static HttpResponseMessage Html(byte[] body, string? charset) {
        HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(body),
        };

        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html") {
            CharSet = charset,
        };

        return response;
    }

    [Fact]
    public async Task FetchAsync_SuccessfulResponse_ReturnsContentAndFetchedAt() {
        // 復号はここで行わない。<meta charset> でのみ宣言するページを読めるよう、
        // 文字コードの判定は抽出側(AngleSharp)に委ねる。
        StubHttpMessageHandler handler = new(Html("<html><body>本文</body></html>"));
        HttpPageFetcher fetcher = CreateFetcher(handler);

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Equal("<html><body>本文</body></html>"u8.ToArray(), actual.Content);
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
        Assert.Null(actual.Content);
        Assert.Contains(((int)status).ToString(), actual.FailureReason);
        Assert.Equal(Now, actual.FetchedAt);
    }

    [Fact]
    public async Task FetchAsync_ShiftJisCharsetInContentType_IsReportedWithoutDecoding() {
        // 日本語サイトでは Shift_JIS が現役で使われている。
        // 復号せず、申告された名前をそのまま抽出側へ渡す。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] body = Encoding.GetEncoding("shift_jis").GetBytes("<html><body>日本語</body></html>");

        HttpPageFetcher fetcher = CreateFetcher(new StubHttpMessageHandler(Html(body, "shift_jis")));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(body, actual.Content);
        Assert.Equal("shift_jis", actual.DeclaredCharset);
    }

    [Fact]
    public async Task FetchAsync_NoCharsetInContentType_ReportsNoDeclaration() {
        // 申告が無いことと、申告があることを取り違えてはならない。
        // 申告として何かを渡すと、AngleSharpがページ内の宣言より優先してしまう。
        HttpPageFetcher fetcher = CreateFetcher(
            new StubHttpMessageHandler(Html([0x41], charset: null)));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Null(actual.DeclaredCharset);
    }

    [Fact]
    public async Task FetchAsync_UnresolvableCharset_IsNotTreatedAsFailure() {
        // 解釈できない名前を理由にページを捨てない。判定の余地を抽出側に残す。
        byte[] body = [0x93, 0xfa, 0x96, 0x7b];

        HttpPageFetcher fetcher = CreateFetcher(
            new StubHttpMessageHandler(Html(body, "x-unknown-encoding")));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Equal(body, actual.Content);
        Assert.Equal("x-unknown-encoding", actual.DeclaredCharset);
    }

    [Fact]
    public async Task FetchAsync_ContentLengthExceedsLimit_ReturnsFailureWithoutReadingBody() {
        // 申告の時点で分かる場合は、本文を読まずに諦める。
        RecordingStream body = new(new byte[MaxPageBytes + 1]);

        HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new StreamContent(body),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        response.Content.Headers.ContentLength = MaxPageBytes + 1;

        HttpPageFetcher fetcher = CreateFetcher(new StubHttpMessageHandler(response));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Contains("サイズ", actual.FailureReason);
        Assert.False(body.WasRead);
    }

    /// <summary>
    /// 読み込まれたかどうかを記録する <see cref="MemoryStream"/>。
    /// </summary>
    /// <remarks>
    /// 応答の破棄でストリームは閉じられるため、位置ではなくこの記録で判定する。
    /// </remarks>
    private sealed class RecordingStream(byte[] content) : MemoryStream(content) {
        public bool WasRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) {
            WasRead = true;

            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            WasRead = true;

            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    [Fact]
    public async Task FetchAsync_BodyExceedsLimitWithoutContentLength_ReturnsFailure() {
        // Content-Length は省略できるうえ、実際の本文と一致する保証もない。
        // 読み込みながら上限を確かめる必要がある。
        HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[MaxPageBytes + 1]),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        response.Content.Headers.ContentLength = null;

        HttpPageFetcher fetcher = CreateFetcher(new StubHttpMessageHandler(response));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Contains("サイズ", actual.FailureReason);
    }

    [Fact]
    public async Task FetchAsync_BodyAtExactLimit_Succeeds() {
        HttpPageFetcher fetcher = CreateFetcher(
            new StubHttpMessageHandler(Html(new byte[MaxPageBytes], charset: null)));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Equal(MaxPageBytes, actual.Content.Length);
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
    public async Task FetchAsync_ConnectionLostWhileReadingBody_ReturnsFailureInsteadOfThrowing() {
        // ResponseHeadersRead を指定しているため、本文はヘッダの受信後に読む。
        // この段階の切断は HttpRequestException ではなく HttpIOException(IOException の派生)になる。
        HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new StreamContent(new FailingStream()),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        HttpPageFetcher fetcher = CreateFetcher(new StubHttpMessageHandler(response));

        PageFetchResult actual = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.False(actual.IsSuccess);
        Assert.Contains(FailingStream.Message, actual.FailureReason);
    }

    /// <summary>
    /// 読み込もうとすると接続の切断を表す例外を投げる <see cref="MemoryStream"/>。
    /// </summary>
    private sealed class FailingStream : MemoryStream {
        public const string Message = "応答の途中で接続が切断された";

        public override int Read(byte[] buffer, int offset, int count) {
            throw new HttpIOException(HttpRequestError.ResponseEnded, Message);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            throw new HttpIOException(HttpRequestError.ResponseEnded, Message);
        }
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
