using System;

namespace MkRss.Web;

/// <summary>
/// 設定ファイルおよび環境変数から読み込む動作設定。
/// </summary>
/// <param name="UserAgent">
/// ページ取得時に <c>User-Agent</c> ヘッダとして送信する文字列。
/// 巡回先の管理者が連絡できるよう、連絡先のURLを含めてよい。
/// </param>
/// <param name="RobotsProductToken">
/// robots.txt の User-agent グループを選ぶ際に使う product token(例 : <c>mkrss</c>)。
/// <paramref name="UserAgent"/> とは役割が異なるため別の設定にしている。
/// RFC 9309 は product token を英字・アンダースコア・ハイフンのみと定めており、
/// 記号を含む完全な User-Agent 文字列はグループ名として成立しない。
/// </param>
/// <param name="FetchTimeout">1ページあたりの取得を諦めるまでの時間。</param>
/// <param name="MaxPageBytes">
/// 1ページあたりに読み込むバイト数の上限。超えた場合はそのサイトの取得を失敗として扱う。
/// 巡回先が巨大な応答を返したときに、タイムアウト内であってもメモリを使い切らないようにする。
/// </param>
/// <param name="FeedLimit">1フィードに含める記事の最大件数。</param>
/// <param name="ApiToken">
/// <c>/api/</c> 配下を呼び出す際に必要な共有シークレット。
/// リバースプロキシで外部に公開しない前提の多層防御であり、これ単独で守る想定ではない。
/// </param>
/// <param name="PublicBaseUrl">
/// フィードを公開するURLの基底(例 : <c>https://feeds.example.com</c>)。
/// リバースプロキシの背後ではリクエストのスキームが <c>http</c> になるため、
/// Atomの <c>rel="self"</c> をリクエストから組み立てると誤った値になる。
/// 設定しない場合はリクエストのURLをそのまま使う(ローカル開発向け)。
/// </param>
public sealed record MkRssOptions(
    string UserAgent,
    string RobotsProductToken,
    TimeSpan FetchTimeout,
    int MaxPageBytes,
    int FeedLimit,
    string ApiToken,
    string? PublicBaseUrl = null) {
    /// <summary>設定の節の名前。</summary>
    public const string SectionName = "MkRss";

    /// <summary>
    /// 設定値を検証する。不正な値があれば <see cref="InvalidOperationException"/> を投げる。
    /// </summary>
    /// <remarks>
    /// 起動時に失敗させる。いずれの項目も、不正なまま起動すると例外にはならず、
    /// 「取得できない」「拒否を無視する」といった形で実行時に現れるため、
    /// 動かしてみるまで気づけない。
    /// </remarks>
    public void Validate() {
        RequireText(UserAgent, nameof(UserAgent));
        RequireText(ApiToken, nameof(ApiToken));
        RequireProductToken(RobotsProductToken);

        // 0 だと全ページが即タイムアウトする。
        RequirePositive(FetchTimeout > TimeSpan.Zero, nameof(FetchTimeout), FetchTimeout);

        // 0 だと全ページがサイズ上限の超過で失敗する。
        RequirePositive(MaxPageBytes > 0, nameof(MaxPageBytes), MaxPageBytes);

        // 0 だと記事が1件も含まれないフィードを配信し続ける。
        RequirePositive(FeedLimit > 0, nameof(FeedLimit), FeedLimit);

        RequireAbsoluteUrl(PublicBaseUrl);
    }

    private static void RequireText(string? value, string name) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"設定 '{SectionName}:{name}' が設定されていない。");
        }
    }

    /// <summary>
    /// product token が RFC 9309 の定める字種に収まっているかを確かめる。
    /// </summary>
    /// <remarks>
    /// 記号を含む値(完全な User-Agent 文字列など)を設定しても例外にはならず、
    /// 名指しのグループに一致しないという形でのみ現れる。ここで弾かないと気づけない。
    /// </remarks>
    private static void RequireProductToken(string? value) {
        RequireText(value, nameof(RobotsProductToken));

        foreach (char character in value!) {
            if (!char.IsAsciiLetter(character) && character != '_' && character != '-') {
                throw new InvalidOperationException(
                    $"設定 '{SectionName}:{nameof(RobotsProductToken)}' に使えない文字 '{character}' が含まれている。"
                    + " RFC 9309 は product token を英字・アンダースコア・ハイフンのみと定めている。");
            }
        }
    }

    private static void RequirePositive(bool isPositive, string name, object value) {
        if (!isPositive) {
            throw new InvalidOperationException(
                $"設定 '{SectionName}:{name}' は正の値である必要がある。現在の値 : {value}");
        }
    }

    /// <summary>
    /// 公開URLの基底が絶対URLかを確かめる。未設定は許す。
    /// </summary>
    /// <remarks>
    /// 不正な値でも起動は成功し、フィードを配信する時点で初めて例外になる。
    /// </remarks>
    private static void RequireAbsoluteUrl(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)) {
            throw new InvalidOperationException(
                $"設定 '{SectionName}:{nameof(PublicBaseUrl)}' は http または https の絶対URLである必要がある。"
                + $" 現在の値 : {value}");
        }
    }
}
