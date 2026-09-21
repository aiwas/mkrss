using System;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Fetching;

/// <summary>
/// 巡回対象のページを取得する。
/// </summary>
public interface IPageFetcher {
    /// <summary>
    /// <paramref name="url"/> のHTMLを取得する。
    /// </summary>
    /// <param name="url">取得するページのURL。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken);
}
