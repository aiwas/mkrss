using MkRss.Core.Extraction;

namespace MkRss.Core.Sites;

/// <summary>
/// 管理UIのフォームが編集するサイトの内容。
/// </summary>
/// <remarks>
/// <c>id</c> と作成・更新日時を持たない。新規登録と更新の両方でこの型を検証するため、
/// まだ採番されていない識別子を表現する必要がないようにしている。
/// 巡回に使う <see cref="Crawling.CrawlTarget"/> とは役割が異なるため別の型にしている。
/// </remarks>
/// <param name="Slug">フィードの公開URLに使う名前。</param>
/// <param name="Name">サイト名。</param>
/// <param name="PageUrl">
/// 巡回する一覧ページのURL。フォームが入力した文字列をそのまま持つ。
/// 絶対URLとして解釈できるかは <see cref="SiteValidator"/> が確かめる。
/// </param>
/// <param name="IsEnabled">巡回の対象にするか。配信の可否には影響しない。</param>
/// <param name="Rule">このページから記事を抽出する規則。</param>
public sealed record SiteDraft(
    string Slug,
    string Name,
    string PageUrl,
    bool IsEnabled,
    ExtractionRule Rule);
