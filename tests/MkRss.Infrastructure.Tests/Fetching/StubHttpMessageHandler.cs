using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Infrastructure.Tests.Fetching;

/// <summary>
/// 応答を差し替えられる <see cref="HttpMessageHandler"/>。ネットワークには接続しない。
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<HttpRequestMessage> _requests = [];

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) {
        _respond = respond;
    }

    public StubHttpMessageHandler(HttpResponseMessage response)
        : this((_, _) => Task.FromResult(response)) {
    }

    /// <summary>受け取ったリクエスト。送信したヘッダの検証に使う。</summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        _requests.Add(request);

        return _respond(request, cancellationToken);
    }
}
