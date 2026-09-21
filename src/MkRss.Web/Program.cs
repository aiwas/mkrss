using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MkRss.Core.Crawling;
using MkRss.Core.Feeds;
using MkRss.Core.Fetching;
using MkRss.Infrastructure.Fetching;
using MkRss.Infrastructure.Hosting;
using MkRss.Infrastructure.Persistence;
using MkRss.Web.Endpoints;
using Npgsql;

namespace MkRss.Web;

public class Program {
    /// <summary>
    /// DBの接続文字列を置く設定キー。環境変数では <c>Supabase__ConnectionStrings</c> になる。
    /// </summary>
    private const string ConnectionStringKey = "Supabase:ConnectionStrings";

    public static async Task Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        MkRssOptions options = builder.Configuration
            .GetSection(MkRssOptions.SectionName)
            .Get<MkRssOptions>()
            ?? throw new InvalidOperationException($"設定 '{MkRssOptions.SectionName}' が見つからない。");

        // 未設定のまま起動すると、空文字どうしの比較が成立して認証が素通りする。
        if (string.IsNullOrEmpty(options.ApiToken)) {
            throw new InvalidOperationException($"設定 '{MkRssOptions.SectionName}:ApiToken' が設定されていない。");
        }

        // 接続文字列はDBパスワードを含むため全体がシークレット。
        // 開発時は user-secrets、本番は環境変数から読み込む。
        // 既定の ConnectionStrings 節ではないため、GetConnectionString は使えない。
        string connectionString = builder.Configuration[ConnectionStringKey]
            ?? throw new InvalidOperationException($"設定 '{ConnectionStringKey}' が設定されていない。");

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);

        // NpgsqlDataSource は接続プールを持つ。DIのライフタイムに載せて確実に破棄する。
        builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        builder.Services.AddSingleton(new PageFetcherOptions(options.UserAgent, options.FetchTimeout));
        builder.Services.AddHttpClient<IPageFetcher, HttpPageFetcher>();

        builder.Services.AddScoped<ICrawlTargetSource, NpgsqlCrawlTargetSource>();
        builder.Services.AddScoped<IArticleStore, NpgsqlArticleStore>();
        builder.Services.AddScoped<ICrawlRunLog, NpgsqlCrawlRunLog>();
        builder.Services.AddScoped<IFeedSource, NpgsqlFeedSource>();
        builder.Services.AddSingleton(new CrawlerOptions(options.UserAgent));
        builder.Services.AddScoped<Crawler>();
        builder.Services.AddScoped<CrawlJob>();

        builder.Services.AddSingleton<CrawlQueue>();
        builder.Services.AddHostedService<CrawlBackgroundService>();

        WebApplication app = builder.Build();

        app.MapCrawlApi();
        app.MapFeeds();

        await app.RunAsync();
    }
}
