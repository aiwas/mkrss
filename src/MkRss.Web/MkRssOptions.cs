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
    int FeedLimit,
    string ApiToken,
    string? PublicBaseUrl = null) {
    /// <summary>設定の節の名前。</summary>
    public const string SectionName = "MkRss";
}
