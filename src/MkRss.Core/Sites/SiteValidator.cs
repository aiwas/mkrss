using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MkRss.Core.Extraction;

namespace MkRss.Core.Sites;

/// <summary>
/// 管理UIが受け取った入力を、保存できる形かどうか確かめる。
/// </summary>
/// <remarks>
/// DBのCHECK制約と同じことを確かめる項目がある。違反を例外ではなく
/// 入力欄ごとの指摘として返すために、手前で同じ判定を行う。
/// <c>sites_date_pair_check</c> に対応する項目は無い。
/// C#側では日付の取得方法を <see cref="FieldSelector"/> の有無で表すため、
/// 「セレクタが無いのに属性名だけある」という状態を作れない。
/// </remarks>
public static partial class SiteValidator {
    /// <summary>
    /// <paramref name="draft"/> を検証し、見つかった問題をすべて返す。
    /// 問題が無ければ空を返す。
    /// </summary>
    /// <remarks>
    /// 最初の1件で打ち切らない。入力欄ごとに指摘を並べて示すため。
    /// </remarks>
    public static IReadOnlyList<SiteValidationError> Validate(SiteDraft draft) {
        List<SiteValidationError> errors = [];

        if (!SlugFormat().IsMatch(draft.Slug)) {
            errors.Add(new(SiteField.Slug, "slug はアルファベット小文字・数字・ハイフンのみで入力してください。"));
        }

        if (string.IsNullOrWhiteSpace(draft.Name)) {
            errors.Add(new(SiteField.Name, "サイト名を入力してください。"));
        }

        if (!IsHttpUrl(draft.PageUrl)) {
            errors.Add(new(SiteField.PageUrl, "一覧ページのURLは http または https の絶対URLで入力してください。"));
        }

        // 空のHTML文書をセレクタの検証に使う。
        // 抽出と同じ QuerySelector に通すことで、巡回時に受け付けられる指定かを確かめる。
        // HtmlParser と文書はスレッド安全性が保証されないため、呼び出しごとに作る。
        using IHtmlDocument document = new HtmlParser().ParseDocument(string.Empty);

        if (string.IsNullOrWhiteSpace(draft.Rule.ItemSelector)) {
            errors.Add(new(SiteField.ItemSelector, "記事1件を表す要素のセレクタを入力してください。"));
        } else {
            AddSelectorError(errors, document, SiteField.ItemSelector, draft.Rule.ItemSelector);
        }

        AddFieldSelectorError(errors, document, SiteField.TitleSelector, draft.Rule.Title);
        AddFieldSelectorError(errors, document, SiteField.LinkSelector, draft.Rule.Link);
        AddFieldSelectorError(errors, document, SiteField.DateSelector, draft.Rule.Date);

        return errors;
    }

    /// <summary>
    /// 値の取得方法に含まれるセレクタを検証する。
    /// </summary>
    /// <remarks>
    /// 取得方法そのものが無い(日付を抽出しない)場合と、
    /// 空文字(記事要素自身を対象とする)の場合は検証しない。
    /// 後者はCSSとして解釈できない文字列だが、抽出側が
    /// <see cref="string.Length"/> が 0 かどうかで先に分岐するため問題にならない。
    /// </remarks>
    private static void AddFieldSelectorError(
        List<SiteValidationError> errors,
        IHtmlDocument document,
        SiteField field,
        FieldSelector? selector) {
        if (selector is null || selector.Selector.Length == 0) {
            return;
        }

        AddSelectorError(errors, document, field, selector.Selector);
    }

    private static void AddSelectorError(
        List<SiteValidationError> errors,
        IHtmlDocument document,
        SiteField field,
        string selector) {
        if (!IsParsableSelector(document, selector)) {
            errors.Add(new SiteValidationError(field, "CSSセレクタとして解釈できない。"));
        }
    }

    /// <summary>
    /// <paramref name="selector"/> をCSSセレクタとして解釈できるかを確かめる。
    /// </summary>
    /// <remarks>
    /// 例外の型は抽出に使うライブラリの都合で決まるため、型を限定せずに受け止める。
    /// </remarks>
    private static bool IsParsableSelector(IHtmlDocument document, string selector) {
        try {
            document.QuerySelectorAll(selector);

            return true;
        } catch (Exception) {
            return false;
        }
    }

    /// <summary>
    /// <c>sites_slug_format_check</c> と同じ形式。
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();

    private static bool IsHttpUrl(string value) {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);
    }
}
