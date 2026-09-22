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
            Encoding.UTF8.GetBytes(content), declaredCharset: null, FetchedAt);
    }

    /// <summary>任意のバイト列と申告内容を返すよう登録する。</summary>
    public void SetContent(Uri url, byte[] content, string? declaredCharset) {
        _responses[url] = PageFetchResult.Success(content, declaredCharset, FetchedAt);
    }

    public void SetFailure(Uri url, string reason) {
        _responses[url] = PageFetchResult.Failure(reason, FetchedAt);
    }

    public Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        RequestedUrls.Add(url);

        // 登録していないURLは取得失敗として扱う。robots.txt が存在しないサイトを表現できる。
        return Task.FromResult(_responses.TryGetValue(url, out PageFetchResult? response)
            ? response
            : PageFetchResult.Failure("登録されていないURL", FetchedAt));
    }
}
