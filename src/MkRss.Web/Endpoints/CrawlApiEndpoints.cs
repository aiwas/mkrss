using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MkRss.Core.Crawling;
using MkRss.Infrastructure.Hosting;

namespace MkRss.Web.Endpoints;

/// <summary>
/// クロールの実行と状態確認のエンドポイント。
/// </summary>
public static class CrawlApiEndpoints {
    /// <summary>
    /// <c>/api/</c> 配下を登録する。
    /// </summary>
    public static IEndpointRouteBuilder MapCrawlApi(this IEndpointRouteBuilder endpoints) {
        // 認証はグループに対して1回だけ設定する。
        // エンドポイントごとに付ける形だと、追加時に付け忘れたものが素通りする。
        RouteGroupBuilder api = endpoints
            .MapGroup("/api")
            .AddEndpointFilter<ApiTokenFilter>();

        api.MapPost("/crawl", RequestCrawl);
        api.MapGet("/status", GetStatusAsync);

        return endpoints;
    }

    /// <summary>
    /// クロールを要求する。
    /// </summary>
    /// <remarks>
    /// 完走まで数分かかりうるため、ここでは受け付けたことだけを返す。
    /// HTTPリクエスト内で完走させると、リバースプロキシのタイムアウトにより
    /// CRON側が失敗と判断し、再実行で多重クロールが発生する。
    /// </remarks>
    private static IResult RequestCrawl(CrawlQueue queue) {
        return queue.TryEnqueue()
            ? Results.Accepted()
            : Results.Conflict(new { message = "クロールがすでに実行中。" });
    }

    /// <summary>
    /// 直近の実行結果を返す。
    /// </summary>
    /// <remarks>
    /// CRONは <c>202 Accepted</c> を受け取るだけでクロールの成否を知れない。
    /// exit code による失敗検知の代替としてこの記録を参照する。
    /// </remarks>
    private static async Task<IResult> GetStatusAsync(
        ICrawlRunLog log,
        CancellationToken cancellationToken) {
        CrawlRunStatus? latest = await log.FindLatestAsync(cancellationToken);

        return latest is null ? Results.NoContent() : Results.Ok(latest);
    }
}
