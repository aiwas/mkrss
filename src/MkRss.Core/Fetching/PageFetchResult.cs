using System;
using System.Diagnostics.CodeAnalysis;

namespace MkRss.Core.Fetching;

/// <summary>
/// ページ取得の結果。
/// 取得に失敗しても例外を投げず、この型で失敗を表現する。
/// 1サイトの失敗をどう扱うかを呼び出し側が決められるようにするため。
/// </summary>
/// <param name="Content">
/// 取得したバイト列。失敗した場合は <see langword="null"/>。
/// </param>
/// <param name="DeclaredCharset">
/// HTTPの応答が申告した文字コードの名前。申告が無かった場合と失敗した場合は <see langword="null"/>。
/// </param>
/// <param name="FinalUrl">
/// 内容を返したURL。リダイレクトを辿った場合は、要求したURLではなく最後のURLになる。
/// 失敗した場合は <see langword="null"/>。
/// </param>
/// <param name="FailureReason">失敗の理由。成功した場合は <see langword="null"/>。</param>
/// <param name="FailureStatusCode">
/// 失敗の原因となった応答のステータスコード。成功した場合と、
/// 応答を受け取れずに失敗した場合(ネットワークエラー、タイムアウトなど)は <see langword="null"/>。
/// </param>
/// <param name="FetchedAt">取得を試みた日時。</param>
/// <remarks>
/// 復号せずバイト列のまま返す。HTTPヘッダには文字コードを書かず、
/// HTML内の <c>&lt;meta charset&gt;</c> でのみ宣言するページがあるため、
/// 取得の時点で復号すると、その宣言を読む機会が失われる。
/// </remarks>
public sealed record PageFetchResult(
    byte[]? Content,
    string? DeclaredCharset,
    Uri? FinalUrl,
    string? FailureReason,
    int? FailureStatusCode,
    DateTimeOffset FetchedAt) {
    /// <summary>取得に成功したかどうか。</summary>
    [MemberNotNullWhen(true, nameof(Content), nameof(FinalUrl))]
    public bool IsSuccess => Content is not null && FinalUrl is not null;

    /// <summary>成功した結果を作る。</summary>
    public static PageFetchResult Success(
        byte[] content,
        string? declaredCharset,
        Uri finalUrl,
        DateTimeOffset fetchedAt) {
        return new PageFetchResult(content, declaredCharset, finalUrl, null, null, fetchedAt);
    }

    /// <summary>失敗した結果を作る。</summary>
    public static PageFetchResult Failure(string reason, DateTimeOffset fetchedAt, int? statusCode = null) {
        return new PageFetchResult(null, null, null, reason, statusCode, fetchedAt);
    }
}
