using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MkRss.Core.Crawling;
using MkRss.Core.Feeds;
using MkRss.Core.Fetching;
using MkRss.Core.Sites;
using MkRss.Infrastructure.Fetching;
using MkRss.Infrastructure.Hosting;
using MkRss.Infrastructure.Persistence;
using MkRss.Web.Components;
using MkRss.Web.Endpoints;
using MudBlazor.Services;
using Npgsql;

namespace MkRss.Web;

public class Program {
    /// <summary>
    /// DBの接続文字列を置く設定キー。環境変数では <c>Supabase__ConnectionStrings</c> になる。
    /// </summary>
    private const string ConnectionStringKey = "Supabase:ConnectionStrings";

    public static async Task Main(string[] args) {
        // Shift_JIS や EUC-JP を申告するページを復号できるようにする。
        // .NET が既定で解決できるのは UTF 系と ASCII のみで、日本語のサイトを扱う前提では足りない。
        // AngleSharp もアセンブリの読み込み時に同じ登録を行うが、その副作用に依存すると
        // 「最初の抽出より前に取得したページだけ失敗する」という順序依存の挙動になる。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        MkRssOptions options = builder.Configuration
            .GetSection(MkRssOptions.SectionName)
            .Get<MkRssOptions>()
            ?? throw new InvalidOperationException($"設定 '{MkRssOptions.SectionName}' が見つからない。");

        // 不正な設定は起動時に失敗させる。いずれの項目も、そのまま起動すると例外にはならず
        // 実行時の挙動としてのみ現れるため、動かしてみるまで気づけない。
        // 例 : ApiToken が空だと、空文字どうしの比較が成立して認証が素通りする。
        options.Validate();

        // 接続文字列はDBパスワードを含むため全体がシークレット。
        // 開発時は user-secrets、本番は環境変数から読み込む。
        // 既定の ConnectionStrings 節ではないため、GetConnectionString は使えない。
        string connectionString = builder.Configuration[ConnectionStringKey]
            ?? throw new InvalidOperationException($"設定 '{ConnectionStringKey}' が設定されていない。");

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);

        // NpgsqlDataSource は接続プールを持つ。DIのライフタイムに載せて確実に破棄する。
        builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        builder.Services.AddSingleton(
            new PageFetcherOptions(options.UserAgent, options.FetchTimeout, options.MaxPageBytes));
        builder.Services.AddHttpClient<IPageFetcher, HttpPageFetcher>();

        // Blazor Server の管理UI。APIとフィード配信と同じアプリに同居させる。
        // Blazor では Scoped の寿命がリクエストごとではなくサーキット単位になる。
        // ここに登録する実装は接続を保持しない、無状態のものに限ること。
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddMudServices();

        builder.Services.AddScoped<ISiteRepository, NpgsqlSiteRepository>();
        builder.Services.AddScoped<ICrawlTargetSource, NpgsqlCrawlTargetSource>();
        builder.Services.AddScoped<IArticleStore, NpgsqlArticleStore>();
        builder.Services.AddScoped<ICrawlRunLog, NpgsqlCrawlRunLog>();
        builder.Services.AddScoped<IFeedSource, NpgsqlFeedSource>();
        // robots.txt の判定には product token を渡す。送信する User-Agent とは別の値。
        builder.Services.AddSingleton(new CrawlerOptions(options.RobotsProductToken));
        builder.Services.AddScoped<Crawler>();
        builder.Services.AddScoped<CrawlJob>();

        builder.Services.AddSingleton<CrawlQueue>();
        builder.Services.AddHostedService<CrawlBackgroundService>();

        WebApplication app = builder.Build();

        app.MapStaticAssets();
        app.UseAntiforgery();

        app.MapLanding();
        app.MapCrawlApi();
        app.MapFeeds();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        await app.RunAsync();
    }
}
