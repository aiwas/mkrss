using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Crawling;
using MkRss.Core.Extraction;

namespace MkRss.Core.Tests.Crawling;

/// <summary>
/// 保存内容を記録するだけの <see cref="IArticleStore"/>。
/// </summary>
/// <param name="newArticleCount">
/// 新規として報告する件数。<see langword="null"/> の場合は渡された件数をそのまま返す。
/// </param>
internal sealed class FakeArticleStore(int? newArticleCount = null) : IArticleStore {
    /// <summary>保存を要求された内容を、要求された順に記録する。</summary>
    public List<(Guid SiteId, IReadOnlyList<ExtractedArticle> Articles)> Saved { get; } = [];

    public Task<int> SaveNewAsync(
        Guid siteId,
        IReadOnlyList<ExtractedArticle> articles,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        Saved.Add((siteId, articles));

        return Task.FromResult(newArticleCount ?? articles.Count);
    }
}
