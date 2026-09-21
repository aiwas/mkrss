using System;

namespace MkRss.Core.Extraction;

/// <summary>
/// 1つのウェブサイトから記事情報を抽出するための規則。
/// </summary>
/// <param name="ItemSelector">記事1件を表す要素を選択するCSSセレクタ。</param>
/// <param name="Title">記事タイトルの取得方法。</param>
/// <param name="Link">記事URLの取得方法。通常は <c>href</c> 属性を指定する。</param>
/// <param name="Date">
/// 記事日付の取得方法。<see langword="null"/> の場合は日付を抽出せず、取得日時を使う。
/// </param>
/// <param name="DateOffset">
/// タイムゾーンを持たない日付表記に適用するオフセット。
/// </param>
public sealed record ExtractionRule(
    string ItemSelector,
    FieldSelector Title,
    FieldSelector Link,
    FieldSelector? Date,
    TimeSpan DateOffset);
