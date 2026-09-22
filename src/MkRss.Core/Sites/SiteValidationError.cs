namespace MkRss.Core.Sites;

/// <summary>
/// 検証で見つかった問題を1件表す。
/// </summary>
/// <param name="Field">問題のある入力欄。</param>
/// <param name="Message">利用者に示す内容。</param>
public sealed record SiteValidationError(SiteField Field, string Message);

/// <summary>
/// 検証の対象となる入力欄。
/// </summary>
/// <remarks>
/// 列名の文字列ではなく列挙で表す。管理UIが問題のある欄を特定するために使う値であり、
/// 文字列で受け渡すと綴りの誤りがコンパイル時に検出されない。
/// </remarks>
public enum SiteField {
    /// <summary><c>slug</c> 列。</summary>
    Slug,

    /// <summary><c>name</c> 列。</summary>
    Name,

    /// <summary><c>page_url</c> 列。</summary>
    PageUrl,

    /// <summary><c>item_selector</c> 列。</summary>
    ItemSelector,

    /// <summary><c>title_selector</c> 列。</summary>
    TitleSelector,

    /// <summary><c>link_selector</c> 列。</summary>
    LinkSelector,

    /// <summary><c>date_selector</c> 列。</summary>
    DateSelector,
}
