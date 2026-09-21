using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Core.Extraction;

namespace MkRss.Core.Crawling;

/// <summary>
/// 抽出した記事を保存する。
/// </summary>
public interface IArticleStore {
    /// <summary>
    /// <paramref name="articles"/> のうち、まだ保存されていないものだけを追加する。
    /// </summary>
    /// <returns>実際に追加された件数。</returns>
    /// <remarks>
    /// 既に保存済みの記事が含まれていても失敗させない。
    /// クロールが途中で中断しても、次回の実行で自然に回復できるようにするため。
    /// </remarks>
    Task<int> SaveNewAsync(
        Guid siteId,
        IReadOnlyList<ExtractedArticle> articles,
        CancellationToken cancellationToken);
}
