using System;
using System.Collections.Generic;
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

    public void SetContent(Uri url, string content) {
        _responses[url] = PageFetchResult.Success(content, FetchedAt);
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
