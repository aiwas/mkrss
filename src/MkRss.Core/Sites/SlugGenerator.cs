using System;
using System.Security.Cryptography;
using System.Text;

namespace MkRss.Core.Sites;

/// <summary>
/// サイトの登録時に <c>slug</c> の初期値を組み立てる。
/// </summary>
/// <remarks>
/// 生成するのは初期値であり、登録後は自由に編集できる値として扱う。
/// 結果は必ず <c>sites_slug_format_check</c> に適合する形になる。
/// </remarks>
public static class SlugGenerator {
    /// <summary>slug の語をつなぐ文字。</summary>
    private const char Separator = '-';

    /// <summary>ランダム接尾辞のバイト数。16進にすると倍の文字数になる。</summary>
    private const int SuffixByteCount = 4;

    /// <summary>
    /// <paramref name="pageUrl"/> と <paramref name="suffix"/> から slug を組み立てる。
    /// </summary>
    /// <param name="pageUrl">巡回する一覧ページのURL。</param>
    /// <param name="suffix">末尾に付けるランダム接尾辞。<see cref="NewSuffix"/> で作る。</param>
    public static string Generate(Uri pageUrl, string suffix) {
        var builder = new StringBuilder();

        // IdnHost は非ASCIIのホスト名を punycode で返す。
        // Host のままだと、非ASCIIの文字がすべて区切りに落ちてホスト名が消える。
        AppendWords(builder, pageUrl.IdnHost);

        string[] segments = pageUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments) {
            // パーセントエンコードを含むセグメントは捨てる。
            // そのまま通すと、符号の16進(e3-83-8b など)が slug に残る。
            if (!segment.Contains('%', StringComparison.Ordinal)) {
                AppendWords(builder, segment);
            }
        }

        // 末尾の区切りを落とす。一覧ページのURLはスラッシュで終わることが多い。
        if (builder.Length > 0 && builder[^1] == Separator) {
            builder.Length--;
        }

        return builder.Append(Separator).Append(suffix).ToString();
    }

    /// <summary>
    /// slug の末尾に付けるランダム接尾辞を作る。
    /// </summary>
    /// <remarks>
    /// 衝突しても <c>slug</c> の <c>unique</c> 違反として現れるだけなので、
    /// 作り直せばよい。slug はフィードの公開URLに現れるため、
    /// 推測しにくい値になるよう暗号論的な乱数を使う。
    /// </remarks>
    public static string NewSuffix() {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SuffixByteCount));
    }

    /// <summary>
    /// 英数字だけを残して <paramref name="source"/> を追記する。
    /// </summary>
    private static void AppendWords(StringBuilder builder, string source) {
        AppendSeparator(builder);

        foreach (char character in source.ToLowerInvariant()) {
            if (char.IsAsciiLetterOrDigit(character)) {
                builder.Append(character);
            } else {
                // 英数字以外はすべて区切りとして扱う。
                AppendSeparator(builder);
            }
        }
    }

    /// <summary>
    /// 区切りを追記する。連続する区切りは1つにまとめる。
    /// </summary>
    private static void AppendSeparator(StringBuilder builder) {
        if (builder.Length > 0 && builder[^1] != Separator) {
            builder.Append(Separator);
        }
    }
}
