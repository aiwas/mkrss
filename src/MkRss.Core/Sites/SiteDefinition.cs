using System;

namespace MkRss.Core.Sites;

/// <summary>
/// 登録済みのサイト1件。
/// </summary>
/// <param name="Id">
/// サイトの識別子。Atomの <c>id</c> にも使うため、
/// <c>slug</c> を変えたいだけの理由で行を作り直してはならない。
/// </param>
/// <param name="Draft">編集できる内容。</param>
/// <param name="CreatedAt">登録した日時。</param>
/// <param name="UpdatedAt">最後に更新した日時。</param>
public sealed record SiteDefinition(
    Guid Id,
    SiteDraft Draft,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
