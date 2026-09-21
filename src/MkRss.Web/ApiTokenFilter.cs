using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MkRss.Web;

/// <summary>
/// <c>/api/</c> 配下の呼び出しに共有シークレットを要求する。
/// </summary>
/// <remarks>
/// <c>/api/</c> はリバースプロキシで外部に公開せず、CRONからは 127.0.0.1 に対して呼び出す。
/// これはその前提が崩れた場合に備えた多層防御であり、人間のログインとは別の仕組みとして扱う。
/// </remarks>
public sealed class ApiTokenFilter : IEndpointFilter {
    /// <summary>共有シークレットを載せるヘッダの名前。</summary>
    public const string HeaderName = "X-MkRss-Token";

    private readonly byte[] _expected;

    public ApiTokenFilter(MkRssOptions options) {
        _expected = Encoding.UTF8.GetBytes(options.ApiToken);
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next) {
        if (!IsAuthorized(context.HttpContext.Request)) {
            return Results.Unauthorized();
        }

        return await next(context);
    }

    private bool IsAuthorized(HttpRequest request) {
        string? presented = request.Headers[HeaderName];

        if (presented is null) {
            return false;
        }

        // 一致するまでの時間から値を推測されないよう、長さに依存しない比較を使う。
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), _expected);
    }
}
