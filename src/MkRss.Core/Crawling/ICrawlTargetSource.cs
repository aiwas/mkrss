using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Crawling;

/// <summary>
/// 巡回対象の一覧を提供する。
/// </summary>
public interface ICrawlTargetSource {
    /// <summary>
    /// 有効になっている巡回対象を返す。
    /// </summary>
    Task<IReadOnlyList<CrawlTarget>> ListEnabledAsync(CancellationToken cancellationToken);
}
