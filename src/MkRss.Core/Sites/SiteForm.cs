using System;
using MkRss.Core.Extraction;

namespace MkRss.Core.Sites;

/// <summary>
/// 入力欄に対応する、書き換えできる形のサイト情報。
/// </summary>
/// <remarks>
/// <see cref="SiteDraft"/> は record であり、各項目を個別に書き換えられない。
/// 画面の入力欄と結び付けるためにこの型を挟む。
/// 変換には間違えやすい箇所が2つあるため、UIの側ではなくここに置いてテストで固定する。
/// <list type="bullet">
/// <item>日付を抽出しない指定と、記事要素自身から抽出する指定の区別</item>
/// <item>未入力の属性名(空文字)を「要素のテキストを読む」に読み替えること</item>
/// </list>
/// </remarks>
public sealed class SiteForm {
    /// <summary><c>slug</c> 列。</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary><c>name</c> 列。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary><c>page_url</c> 列。</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary><c>is_enabled</c> 列。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary><c>item_selector</c> 列。</summary>
    public string ItemSelector { get; set; } = string.Empty;

    /// <summary><c>title_selector</c> 列。</summary>
    public string TitleSelector { get; set; } = string.Empty;

    /// <summary><c>title_attribute</c> 列。未入力は要素のテキストを読む指定になる。</summary>
    public string TitleAttribute { get; set; } = string.Empty;

    /// <summary><c>link_selector</c> 列。</summary>
    public string LinkSelector { get; set; } = string.Empty;

    /// <summary><c>link_attribute</c> 列。</summary>
    public string LinkAttribute { get; set; } = "href";

    /// <summary>
    /// 日付を抽出するかどうか。false のとき <c>date_selector</c> は <see langword="null"/> になる。
    /// </summary>
    public bool ExtractsDate { get; set; }

    /// <summary><c>date_selector</c> 列。</summary>
    public string DateSelector { get; set; } = string.Empty;

    /// <summary><c>date_attribute</c> 列。未入力は要素のテキストを読む指定になる。</summary>
    public string DateAttribute { get; set; } = string.Empty;

    /// <summary>
    /// <c>date_offset</c> 列。時間単位で扱う。
    /// </summary>
    /// <remarks>
    /// 列の型は <c>interval</c> なので時間未満のオフセットも保存できるが、
    /// 画面からは時間単位でのみ指定する。
    /// </remarks>
    public int DateOffsetHours { get; set; }

    /// <summary>
    /// 保存済みの内容から入力欄の状態を作る。
    /// </summary>
    public static SiteForm From(SiteDraft draft) {
        ExtractionRule rule = draft.Rule;

        return new SiteForm {
            Slug = draft.Slug,
            Name = draft.Name,
            PageUrl = draft.PageUrl,
            IsEnabled = draft.IsEnabled,
            ItemSelector = rule.ItemSelector,
            TitleSelector = rule.Title.Selector,
            TitleAttribute = rule.Title.Attribute ?? string.Empty,
            LinkSelector = rule.Link.Selector,
            LinkAttribute = rule.Link.Attribute ?? string.Empty,

            // 空文字のセレクタは「記事要素自身から抽出する」指定であり、
            // 抽出しない指定ではない。取り違えると日付が取得日時で代用される。
            ExtractsDate = rule.Date is not null,
            DateSelector = rule.Date?.Selector ?? string.Empty,
            DateAttribute = rule.Date?.Attribute ?? string.Empty,
            DateOffsetHours = (int)rule.DateOffset.TotalHours,
        };
    }

    /// <summary>
    /// 入力欄の状態から保存する内容を作る。
    /// </summary>
    public SiteDraft ToDraft() {
        ExtractionRule rule = new(
            ItemSelector: ItemSelector,
            Title: new FieldSelector(TitleSelector, ToAttribute(TitleAttribute)),
            Link: new FieldSelector(LinkSelector, ToAttribute(LinkAttribute)),
            Date: ExtractsDate
                ? new FieldSelector(DateSelector, ToAttribute(DateAttribute))
                : null,
            DateOffset: TimeSpan.FromHours(DateOffsetHours));

        return new SiteDraft(Slug, Name, PageUrl, IsEnabled, rule);
    }

    /// <summary>
    /// 属性名の入力を <see cref="FieldSelector.Attribute"/> の形に直す。
    /// </summary>
    /// <remarks>
    /// 未入力は「要素のテキストを読む」を意味する <see langword="null"/> にする。
    /// 空文字のまま渡すと、名前が空文字の属性を読む指定になり値を取得できない。
    /// </remarks>
    private static string? ToAttribute(string value) {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
