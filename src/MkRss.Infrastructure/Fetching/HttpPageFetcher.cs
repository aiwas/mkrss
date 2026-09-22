using System;
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

            using HttpResponseMessage response =
                await _httpClient.SendAsync(request, linked.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) {
                string reason = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
                _logger.LogWarning("ページの取得に失敗しました。{Url} {Reason}", url, reason);

                return PageFetchResult.Failure(reason, fetchedAt);
            }

            string html = await response.Content
                .ReadAsStringAsync(linked.Token)
                .ConfigureAwait(false);

            return PageFetchResult.Success(html, fetchedAt);
        }
        // 呼び出し側によるキャンセルは失敗ではないため、この条件から除外して伝播させる。
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            string reason = $"タイムアウト ({_options.Timeout.TotalSeconds:0.#}秒)";
            _logger.LogWarning("ページの取得がタイムアウトしました。{Url} {Timeout}", url, _options.Timeout);

            return PageFetchResult.Failure(reason, fetchedAt);
        } catch (HttpRequestException exception) {
            _logger.LogWarning(exception, "ページの取得に失敗しました。{Url}", url);

            return PageFetchResult.Failure(exception.Message, fetchedAt);
        }
        // Content-Type が申告した charset を解決できない場合、ReadAsStringAsync は
        // UTF-8 へ倒さずこの例外を投げる。捕捉しないと、1サイトの申告内容で
        // クロール全体が止まる(決定事項10)。
        catch (InvalidOperationException exception) {
            _logger.LogWarning(
                exception, "ページの文字コードを解釈できませんでした。{Url}", url);

            return PageFetchResult.Failure($"文字コードを解釈できない: {exception.Message}", fetchedAt);
        }
    }
}
