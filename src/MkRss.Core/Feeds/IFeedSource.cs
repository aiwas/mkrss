using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MkRss.Core.Feeds;

/// <summary>
/// フィードの配信元となるサイトの情報。
/// </summary>
/// <param name="Id">サイトの識別子。Atomの <c>id</c> に使う。</param>
/// <param name="Name">サイト名。フィードのタイトルに使う。</param>
/// <param name="PageUrl">巡回元のページURL。</param>
public sealed record FeedSite(Guid Id, string Name, Uri PageUrl);

/// <summary>
/// 1つのフィードを構成する情報。
/// </summary>
/// <param name="Site">配信元のサイト。</param>
/// <param name="Entries">出力する記事。新しい順に並ぶ。</param>
public sealed record FeedData(FeedSite Site, IReadOnlyList<FeedEntry> Entries);

/// <summary>
/// 配信するフィードの内容を読み出す。
/// </summary>
public interface IFeedSource {
    /// <summary>
    /// <paramref name="slug"/> に対応するフィードを返す。存在しない場合は <see langword="null"/>。
    /// </summary>
    /// <param name="slug">公開URLに含まれる識別子。</param>
    /// <param name="limit">返す記事の最大件数。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <remarks>
    /// <c>is_enabled</c> は巡回するかどうかの設定であり、配信の可否とは独立している。
    /// 巡回を止めたサイトでも、それまでに集めた記事は配信を続ける。
    /// </remarks>
    Task<FeedData?> FindBySlugAsync(string slug, int limit, CancellationToken cancellationToken);
}
