using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;

namespace MkRss.Core.Tests.Crawling;

/// <summary>
/// あらかじめ渡した巡回対象を返すだけの <see cref="ICrawlTargetSource"/>。
/// </summary>
internal sealed class FakeCrawlTargetSource(params CrawlTarget[] targets) : ICrawlTargetSource {
    public Task<IReadOnlyList<CrawlTarget>> ListEnabledAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<CrawlTarget>>(targets);
    }
}
