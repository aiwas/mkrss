using System;
using System.Threading;
using System.Threading.Tasks;
using MkRss.Infrastructure.Hosting;
using Xunit;

namespace MkRss.Infrastructure.Tests.Hosting;

public sealed class CrawlQueueTests {
    [Fact]
    public void TryEnqueue_FirstRequest_IsAccepted() {
        CrawlQueue queue = new();

        Assert.True(queue.TryEnqueue());
    }

    [Fact]
    public void TryEnqueue_WhileAlreadyAccepted_IsRejected() {
        // 多重クロールを防ぐ。定期実行と手動実行が重なった場合も、後から来た方を断る。
        CrawlQueue queue = new();

        queue.TryEnqueue();

        Assert.False(queue.TryEnqueue());
    }

    [Fact]
    public async Task TryEnqueue_WhileRunning_IsRejected() {
        // 要求を取り出した後も、実行が終わるまでは受け付けない。
        CrawlQueue queue = new();

        queue.TryEnqueue();
        await queue.WaitForRequestAsync(CancellationToken.None);

        Assert.False(queue.TryEnqueue());
    }

    [Fact]
    public async Task TryEnqueue_AfterCompletion_IsAcceptedAgain() {
        CrawlQueue queue = new();

        queue.TryEnqueue();
        await queue.WaitForRequestAsync(CancellationToken.None);
        queue.Complete();

        Assert.True(queue.TryEnqueue());
    }

    [Fact]
    public async Task WaitForRequestAsync_ReceivesEnqueuedRequest() {
        CrawlQueue queue = new();

        ValueTask waiting = queue.WaitForRequestAsync(CancellationToken.None);

        Assert.True(queue.TryEnqueue());

        await waiting;
    }

    [Fact]
    public async Task WaitForRequestAsync_Cancelled_Throws() {
        CrawlQueue queue = new();

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await queue.WaitForRequestAsync(cancellation.Token));
    }
}
