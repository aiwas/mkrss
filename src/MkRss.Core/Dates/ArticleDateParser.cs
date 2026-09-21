using System;
using System.Globalization;

namespace MkRss.Core.Dates;

/// <summary>
/// ウェブページから抽出した日付文字列を <see cref="DateTimeOffset"/> へ変換する。
/// </summary>
public static class ArticleDateParser {
    /// <summary>
    /// <paramref name="text"/> を日付として解釈する。解釈できない場合は <see langword="null"/> を返す。
    /// </summary>
    /// <param name="text">ウェブページから抽出した日付文字列。</param>
    /// <param name="offsetForUnzonedValues">
    /// タイムゾーンを持たない入力に適用するオフセット。
    /// </param>
    public static DateTimeOffset? Parse(string text, TimeSpan offsetForUnzonedValues) {
        // NoCurrentDateDefault を指定しない場合、時刻のみの入力("10:30")に実行日が補われてしまい、
        // 再クロールのたびに日付が変わる。日付を含まない入力は解釈不能として扱う。
        const DateTimeStyles styles = DateTimeStyles.RoundtripKind | DateTimeStyles.NoCurrentDateDefault;

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out DateTime parsed)) {
            return null;
        }

        if (parsed.Date == DateTime.MinValue.Date) {
            return null;
        }

        // RoundtripKind を指定すると、入力にタイムゾーン情報が無かった場合のみ Kind が Unspecified になる。
        // Utc / Local の場合は入力側のオフセットが反映済みなので、そのまま DateTimeOffset へ変換する。
        return parsed.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(parsed, offsetForUnzonedValues)
            : new DateTimeOffset(parsed);
    }
}
