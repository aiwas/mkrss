namespace MkRss.Infrastructure.Hosting;

/// <summary>
/// 待ち受けるポートと、それぞれのポートで応答してよいエンドポイントを決める。
/// </summary>
/// <param name="PublicPort">外部に公開するポート。公開を明示したエンドポイントだけに応答する。</param>
/// <param name="AdminPort">管理用のポート。すべてのエンドポイントに応答する。</param>
/// <remarks>
/// <para>
/// 公開範囲をリバースプロキシのパスの規則ではなく、アプリ側のポートで決める。
/// プロキシの規則に頼ると、同じプロキシに広い規則を持つルーターが追加されたときに
/// 管理UIが外に出る。ここで拒否しておけば、プロキシの設定に依らない。
/// </para>
/// <para>
/// <b>判定には接続を受けたポートを渡すこと。<c>Host</c> ヘッダのポートを渡してはならない。</b>
/// <c>Host</c> はクライアントが自由に書ける値であり、公開用ポートへの接続に
/// 管理用ポートの番号を書かれると管理UIに到達できてしまう。
/// </para>
/// </remarks>
public sealed record ListenerPorts(int PublicPort, int AdminPort) {
    /// <summary>
    /// 接続を受けたポートで、そのエンドポイントに応答してよいかを返す。
    /// </summary>
    /// <param name="localPort">接続を受けたポート。</param>
    /// <param name="isPublicEndpoint">公開を明示したエンドポイントかどうか。</param>
    /// <remarks>
    /// どちらでもないポートはすべて拒否する。設定と異なるポートで待ち受けている場合に、
    /// どちらの扱いにするかを推測しない。
    /// </remarks>
    public bool Allows(int localPort, bool isPublicEndpoint) {
        return localPort == AdminPort || (localPort == PublicPort && isPublicEndpoint);
    }
}
