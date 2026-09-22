using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Sites;

/// <summary>
/// 管理UIから見たサイトの読み書き。
/// </summary>
/// <remarks>
/// 巡回に使う <see cref="Crawling.ICrawlTargetSource"/> とは対象が異なる。
/// あちらは有効な行だけを巡回に必要な形で読み出す。
/// </remarks>
public interface ISiteRepository {
    /// <summary>
    /// 登録済みのサイトをすべて返す。
    /// </summary>
    /// <remarks>
    /// <c>is_enabled</c> で絞り込まない。無効にしたサイトも管理画面には並べる。
    /// </remarks>
    Task<IReadOnlyList<SiteDefinition>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 識別子でサイトを1件返す。存在しない場合は <see langword="null"/>。
    /// </summary>
    Task<SiteDefinition?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// サイトを追加する。
    /// </summary>
    Task<SiteSaveResult> AddAsync(SiteDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// サイトの内容を書き換える。
    /// </summary>
    Task<SiteSaveResult> UpdateAsync(Guid id, SiteDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// 巡回の可否だけを切り替える。
    /// </summary>
    Task SetEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken);
}
