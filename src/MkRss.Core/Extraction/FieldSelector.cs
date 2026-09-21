namespace MkRss.Core.Extraction;

/// <summary>
/// 記事要素から1つの値を取り出すための指定。
/// </summary>
/// <param name="Selector">
/// 記事要素からの相対CSSセレクタ。空文字の場合は記事要素自身を対象とする。
/// </param>
/// <param name="Attribute">
/// 値を取り出す属性名。<see langword="null"/> の場合は要素のテキストを使う。
/// </param>
public sealed record FieldSelector(string Selector, string? Attribute = null);
