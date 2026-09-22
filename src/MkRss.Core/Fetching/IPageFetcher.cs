using System;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Fetching;

/// <summary>
/// 巡回対象のページを取得する。
/// </summary>
public interface IPageFetcher {
    /// <summary>
    /// <paramref name="url"/> の内容を、復号せずバイト列として取得する。
    /// </summary>
    /// <param name="url">取得するページのURL。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <remarks>
    /// 文字コードの判定はここでは行わない。判定に必要な材料(BOM、申告された文字コード、
    /// ページ内の <c>&lt;meta charset&gt;</c>)のうち、最後の1つは復号前のバイト列にしか無いため。
    /// </remarks>
    Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken);
}
