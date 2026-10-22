using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MkRss.Infrastructure.Hosting;

/// <summary>
/// クロールの実行要求を受け付け、バックグラウンドの実行者へ引き渡す。
/// </summary>
/// <remarks>
/// 要求は定期実行と管理UIからの手動実行の2か所から入る。
/// クロールは完走まで数分かかりうるため、要求した側では実行せず、ここを介して引き渡す。
/// 同時に1つしか受け付けないことで多重クロールを防ぐ。
/// プロセス内の状態で判定しているので、複数プロセスで運用する場合は別の仕組みが必要になる。
/// </remarks>
public sealed class CrawlQueue {
    /// <summary>要求の受け渡しに使う。容量は1でよい。待ち行列を作る必要がないため。</summary>
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(1);

    /// <summary>0 なら待機中、1 なら受付済み(実行待ちまたは実行中)。</summary>
    private int _busy;

    /// <summary>
    /// 実行を要求する。すでに受け付け済みの場合は <see langword="false"/> を返す。
    /// </summary>
    public bool TryEnqueue() {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            return false;
        }

        // _busy を獲得できたのはこの呼び出しだけなので、容量1の書き込みは必ず成功する。
        return _channel.Writer.TryWrite(true);
    }

    /// <summary>
    /// 次の実行要求を待つ。
    /// </summary>
    public async ValueTask WaitForRequestAsync(CancellationToken cancellationToken) {
        await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// 実行が終わったことを知らせ、次の要求を受け付けられる状態に戻す。
    /// </summary>
    public void Complete() {
        Interlocked.Exchange(ref _busy, 0);
    }
}
