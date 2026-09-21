using System;

namespace MkRss.Web;

/// <summary>
/// 設定ファイルおよび環境変数から読み込む動作設定。
/// </summary>
/// <param name="UserAgent">
/// ページ取得時に送信し、robots.txt の判定にも使う名前。両者は一致している必要がある。
/// </param>
/// <param name="FetchTimeout">1ページあたりの取得を諦めるまでの時間。</param>
/// <param name="FeedLimit">1フィードに含める記事の最大件数。</param>
/// <param name="ApiToken">
/// <c>/api/</c> 配下を呼び出す際に必要な共有シークレット。
/// リバースプロキシで外部に公開しない前提の多層防御であり、これ単独で守る想定ではない。
/// </param>
public sealed record MkRssOptions(
    string UserAgent,
    TimeSpan FetchTimeout,
    int FeedLimit,
    string ApiToken) {
    /// <summary>設定の節の名前。</summary>
    public const string SectionName = "MkRss";
}
