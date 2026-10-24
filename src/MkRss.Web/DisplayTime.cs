using System;
using System.Globalization;

namespace MkRss.Web;

/// <summary>
/// 管理UIとログに出す日時の書式。両者で同じ表記にするため、ここにまとめる。
/// </summary>
public static class DisplayTime {
    /// <summary>
    /// サーバーのタイムゾーンで表す。オフセットを併記して、どの時刻帯かを明示する。
    /// </summary>
    /// <remarks>
    /// 書式の "/" と ":" はカルチャの区切り文字に置き換えられるため、InvariantCulture で固定する。
    /// </remarks>
    public static string Format(DateTimeOffset value) {
        return value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss '(GMT'zzz')'", CultureInfo.InvariantCulture);
    }
}
