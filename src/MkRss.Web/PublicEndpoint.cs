using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MkRss.Infrastructure.Hosting;

namespace MkRss.Web;

/// <summary>
/// 公開用ポートでも応答してよいエンドポイントに付ける印。
/// </summary>
/// <remarks>
/// 印の無いエンドポイントは管理用ポートでしか応答しない。
/// 公開しないものを列挙する形にすると、管理UIの経路(<c>/_blazor</c> など)が増えたときに漏れる。
/// </remarks>
public sealed class PublicEndpoint {
    private PublicEndpoint() {
    }

    public static PublicEndpoint Instance { get; } = new();
}

/// <summary>
/// 公開用ポートと管理用ポートの振り分けを登録する。
/// </summary>
public static class PublicEndpointExtensions {
    /// <summary>
    /// エンドポイントを公開用ポートでも応答させる。
    /// </summary>
    public static TBuilder AllowOnPublicPort<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder {
        return builder.WithMetadata(PublicEndpoint.Instance);
    }

    /// <summary>
    /// 接続を受けたポートで応答してよくないリクエストを <c>404</c> にする。
    /// </summary>
    /// <remarks>
    /// ルーティングの後に置くこと。エンドポイントが決まっていないと、印の有無を判定できない。
    /// <c>WebApplication</c> はパイプラインの先頭でルーティングを行うため、通常の位置で足りる。
    /// <b>判定には <c>Host</c> ヘッダではなく、接続を受けたポートを使う。</b>
    /// <c>RequireHost</c> は <c>Host</c> ヘッダを見るため、この用途には使えない。
    /// </remarks>
    public static IApplicationBuilder UseListenerPorts(this IApplicationBuilder app, ListenerPorts ports) {
        return app.Use(async (context, next) => {
            bool isPublicEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<PublicEndpoint>() is not null;

            if (!ports.Allows(context.Connection.LocalPort, isPublicEndpoint)) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            await next(context);
        });
    }
}
