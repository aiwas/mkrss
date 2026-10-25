using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Fetching;

namespace MkRss.Core.Tests.Crawling;

/// <summary>
/// URLごとにあらかじめ決めた内容を返す <see cref="IPageFetcher"/>。
/// </summary>
internal sealed class FakePageFetcher : IPageFetcher {
    /// <summary>取得日時として返す固定値。</summary>
    public static readonly DateTimeOffset FetchedAt =
        new(2026, 9, 22, 3, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<Uri, PageFetchResult> _responses = [];

    /// <summary>取得を要求されたURLを、要求された順に記録する。</summary>
    public List<Uri> RequestedUrls { get; } = [];

    /// <summary>UTF-8 のページを返すよう登録する。文字コードの申告は無いものとして扱う。</summary>
    public void SetContent(Uri url, string content) {
        _responses[url] = PageFetchResult.Success(
            Encoding.UTF8.GetBytes(content), declaredCharset: null, url, FetchedAt);
    }

    /// <summary>任意のバイト列と申告内容を返すよう登録する。</summary>
    public void SetContent(Uri url, byte[] content, string? declaredCharset) {
        _responses[url] = PageFetchResult.Success(content, declaredCharset, url, FetchedAt);
    }

    /// <summary>
    /// <paramref name="url"/> へのリダイレクトを辿った結果として、<paramref name="finalUrl"/> の内容を返すよう登録する。
    /// </summary>
    public void SetRedirectedContent(Uri url, Uri finalUrl, string content) {
        _responses[url] = PageFetchResult.Success(
            Encoding.UTF8.GetBytes(content), declaredCharset: null, finalUrl, FetchedAt);
    }

    /// <summary>
    /// 取得に失敗するよう登録する。<paramref name="statusCode"/> を省略すると、
    /// 応答を受け取れなかった失敗(ネットワークエラーなど)になる。
    /// </summary>
    public void SetFailure(Uri url, string reason, int? statusCode = null) {
        _responses[url] = PageFetchResult.Failure(reason, FetchedAt, statusCode);
    }

    public Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        RequestedUrls.Add(url);

        // 登録していないURLは 404 として扱う。robots.txt を設置していないサイトを表現できる。
        return Task.FromResult(_responses.TryGetValue(url, out PageFetchResult? response)
            ? response
            : PageFetchResult.Failure("HTTP 404 Not Found", FetchedAt, 404));
    }
}
