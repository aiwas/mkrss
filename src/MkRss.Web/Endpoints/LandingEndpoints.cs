using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MkRss.Web.Endpoints;

/// <summary>
/// ドメインルートのランディングページ。
/// </summary>
public static class LandingEndpoints {
    /// <summary>
    /// <c>/</c> を登録する。本文は <c>wwwroot/index.html</c>。
    /// </summary>
    /// <remarks>
    /// Blazor のページにはしない。<c>_framework/</c> と <c>_content/</c> の資産を読み込むため、
    /// 公開するとそれらも外に出すことになる。
    /// <c>UseDefaultFiles</c> も使わない。<c>WebApplication</c> が <c>UseRouting</c> を
    /// パイプラインの先頭に置くため、パスの書き換えより先にルートが決まってしまう。
    /// </remarks>
    public static IEndpointRouteBuilder MapLanding(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet("/", (IWebHostEnvironment environment) =>
            TypedResults.PhysicalFile(Path.Combine(environment.WebRootPath, "index.html"), "text/html; charset=utf-8"));

        return endpoints;
    }
}
