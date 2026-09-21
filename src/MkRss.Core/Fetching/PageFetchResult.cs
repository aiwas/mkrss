using System;

namespace MkRss.Core.Fetching;

/// <summary>
/// ページ取得の結果。
/// 取得に失敗しても例外を投げず、この型で失敗を表現する。
/// 1サイトの失敗をどう扱うかを呼び出し側が決められるようにするため。
/// </summary>
/// <param name="Html">取得したHTML。失敗した場合は <see langword="null"/>。</param>
/// <param name="FailureReason">失敗の理由。成功した場合は <see langword="null"/>。</param>
/// <param name="FetchedAt">取得を試みた日時。</param>
public sealed record PageFetchResult(string? Html, string? FailureReason, DateTimeOffset FetchedAt) {
    /// <summary>取得に成功したかどうか。</summary>
    public bool IsSuccess => Html is not null;

    /// <summary>成功した結果を作る。</summary>
    public static PageFetchResult Success(string html, DateTimeOffset fetchedAt) {
        return new PageFetchResult(html, null, fetchedAt);
    }

    /// <summary>失敗した結果を作る。</summary>
    public static PageFetchResult Failure(string reason, DateTimeOffset fetchedAt) {
        return new PageFetchResult(null, reason, fetchedAt);
    }
}
