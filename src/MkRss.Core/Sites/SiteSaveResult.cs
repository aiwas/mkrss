using System;

namespace MkRss.Core.Sites;

/// <summary>
/// サイトの保存結果。
/// <c>slug</c> の重複は例外ではなくこの型で表す。
/// </summary>
/// <remarks>
/// 重複は、ランダム接尾辞の衝突でも利用者の入力でも起きる。
/// どちらも入力欄への指摘として示すべきものであり、異常として扱う理由がない。
/// </remarks>
/// <param name="Id">保存したサイトの識別子。重複した場合は <see cref="Guid.Empty"/>。</param>
/// <param name="IsSlugTaken">同じ <c>slug</c> が既に使われていたかどうか。</param>
public sealed record SiteSaveResult(Guid Id, bool IsSlugTaken) {
    /// <summary>保存できたかどうか。</summary>
    public bool IsSuccess => !IsSlugTaken;

    /// <summary>保存できた結果を作る。</summary>
    public static SiteSaveResult Saved(Guid id) {
        return new SiteSaveResult(id, false);
    }

    /// <summary><c>slug</c> が重複した結果を作る。</summary>
    public static SiteSaveResult SlugTaken() {
        return new SiteSaveResult(Guid.Empty, true);
    }
}
