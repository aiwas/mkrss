using System;
using System.Security.Cryptography;

namespace MkRss.Core.Feeds;

/// <summary>
/// 条件付きGETで使う検証子を作る。
/// </summary>
public static class FeedCacheValidator {
    /// <summary>
    /// 配信する内容から ETag を計算する。
    /// </summary>
    /// <param name="content">配信するXMLのバイト列。</param>
    /// <remarks>
    /// DBの値から組み立てず、実際に返す内容そのものから作る。
    /// 記事の増減だけでなくサイト名の変更でも値が変わるため、
    /// 内容が変わったのに古いものが配信され続ける状態が起きない。
    /// XMLの生成は省略できないが、本文の転送と購読側の解析は省略できる。
    /// </remarks>
    public static string ComputeETag(ReadOnlySpan<byte> content) {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];

        SHA256.HashData(content, hash);

        // 16進表記なので、引用符やバックスラッシュのエスケープは不要。
        return $"\"{Convert.ToHexStringLower(hash)}\"";
    }
}
