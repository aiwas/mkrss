using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkRss.Core.Fetching;

namespace MkRss.Infrastructure.Fetching;

/// <summary>
/// <see cref="HttpClient"/> でページを取得する <see cref="IPageFetcher"/>。
/// </summary>
public sealed class HttpPageFetcher : IPageFetcher {
    /// <summary>1回の読み込みで受け取るバイト数。</summary>
    private const int BufferSize = 8192;

    private readonly HttpClient _httpClient;
    private readonly PageFetcherOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HttpPageFetcher> _logger;

    public HttpPageFetcher(
        HttpClient httpClient,
        PageFetcherOptions options,
        TimeProvider timeProvider,
        ILogger<HttpPageFetcher> logger) {
        _httpClient = httpClient;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken) {
        DateTimeOffset fetchedAt = _timeProvider.GetUtcNow();

        using CancellationTokenSource timeout = new(_options.Timeout);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);

            // ResponseHeadersRead を指定する。既定では本文を全て読み終えてから戻るため、
            // 上限を設けても、そこに到達する前にメモリを消費してしまう。
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) {
                string reason = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
                _logger.LogWarning("ページ取得失敗: {Url} ({Reason})", url, reason);

                return PageFetchResult.Failure(reason, fetchedAt);
            }

            // 申告の時点で分かる場合は、本文を読まずに諦める。
            if (response.Content.Headers.ContentLength > _options.MaxPageBytes) {
                return TooLarge(url, response.Content.Headers.ContentLength, fetchedAt);
            }

            // 復号はここで行わない。ヘッダに文字コードを書かず <meta charset> でのみ
            // 宣言するページがあるため、判定はバイト列を見られる抽出側に委ねる。
            byte[]? content = await ReadWithinLimitAsync(response.Content, linked.Token)
                .ConfigureAwait(false);

            // Content-Length は省略でき、実際の本文と一致する保証もない。読みながらも確かめる。
            if (content is null) {
                return TooLarge(url, null, fetchedAt);
            }

            return PageFetchResult.Success(
                content, response.Content.Headers.ContentType?.CharSet, fetchedAt);
        }
        // 呼び出し側によるキャンセルは失敗ではないため、この条件から除外して伝播させる。
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            string reason = $"タイムアウト ({_options.Timeout.TotalSeconds:0.#}秒)";
            _logger.LogWarning("ページ取得失敗: {Url} ({Reason})", url, reason);

            return PageFetchResult.Failure(reason, fetchedAt);
        }
        // 本文の読み込み中に接続が切れた場合は HttpRequestException ではなく
        // HttpIOException(IOException の派生)になる。捕捉しないとクロール全体が止まる。
        catch (Exception exception) when (exception is HttpRequestException or IOException) {
            _logger.LogWarning(exception, "ページ取得失敗: {Url} ({Reason})", url, exception.Message);

            return PageFetchResult.Failure(exception.Message, fetchedAt);
        }
    }

    /// <summary>
    /// 本文を上限まで読み込む。上限を超えた場合は <see langword="null"/> を返す。
    /// </summary>
    /// <remarks>
    /// 超過を検出した時点で読み込みをやめる。最後まで読んでから長さを確かめる方式では、
    /// 上限を設ける意味が無くなるため。
    /// </remarks>
    private async Task<byte[]?> ReadWithinLimitAsync(
        HttpContent content,
        CancellationToken cancellationToken) {
        await using Stream stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using MemoryStream buffer = new();
        byte[] chunk = new byte[BufferSize];

        while (true) {
            int read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

            if (read == 0) {
                return buffer.ToArray();
            }

            if (buffer.Length + read > _options.MaxPageBytes) {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }

    /// <summary>
    /// サイズ上限の超過を失敗として組み立てる。
    /// </summary>
    private PageFetchResult TooLarge(Uri url, long? contentLength, DateTimeOffset fetchedAt) {
        string reason = $"応答のサイズが上限({_options.MaxPageBytes} バイト)を超えた";

        // 申告が無い場合に "(null)" と出さないよう、文字列にしてから渡す。
        _logger.LogWarning(
            "ページ取得失敗: {Url} (サイズ上限超過、申告: {ContentLength}、上限: {Limit}バイト)",
            url,
            contentLength is null ? "なし" : $"{contentLength}バイト",
            _options.MaxPageBytes);

        return PageFetchResult.Failure(reason, fetchedAt);
    }
}
