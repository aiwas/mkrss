using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using MkRss.Core.Feeds;

namespace MkRss.Web.Endpoints;

/// <summary>
/// フィード配信のエンドポイント。
/// </summary>
public static class FeedEndpoints {
    /// <summary>
    /// フィードの配信先を登録する。
    /// </summary>
    /// <remarks>
    /// 認証は設けない。購読者が誰であるかを問わず配信するため。
    /// 第三者による列挙は slug のランダム接尾辞で防ぐ。
    /// </remarks>
    public static IEndpointRouteBuilder MapFeeds(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/feeds/{slug}.xml", WriteFeedAsync);

        return endpoints;
    }

    private static async Task<IResult> WriteFeedAsync(
        string slug,
        HttpRequest request,
        IFeedSource source,
        MkRssOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) {
        FeedData? feed = await source.FindBySlugAsync(slug, options.FeedLimit, cancellationToken);

        if (feed is null) {
            return Results.NotFound();
        }

        // atom:id は slug ではなくサイトの識別子から作る。slug は後から変更できるため。
        FeedDefinition definition = new(
            Id: feed.Site.Id,
            Title: feed.Site.Name,
            FeedUrl: new Uri(request.GetDisplayUrl()),
            SiteUrl: feed.Site.PageUrl);

        DateTimeOffset updatedAt = feed.Entries.Count > 0
            ? feed.Entries[0].PublishedAt
            : timeProvider.GetUtcNow();

        byte[] content = Encoding.UTF8.GetBytes(
            AtomFeedWriter.Write(definition, feed.Entries, updatedAt));

        // 保存はしてよいが、使う前に必ず問い合わせる。
        // 指定しないと中継側が独自の判断で新鮮とみなし、照合自体が行われないことがある。
        request.HttpContext.Response.Headers.CacheControl = "no-cache";

        // 条件付きGETの判定(If-None-Match / If-Modified-Since)は
        // 検証子を渡すことでフレームワーク側が行う。一致すれば 304 になる。
        return Results.Bytes(
            content,
            "application/atom+xml; charset=utf-8",
            lastModified: updatedAt,
            entityTag: EntityTagHeaderValue.Parse(FeedCacheValidator.ComputeETag(content)));
    }
}
