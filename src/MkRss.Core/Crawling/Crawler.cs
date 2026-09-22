using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkRss.Core.Extraction;
using MkRss.Core.Fetching;
using MkRss.Core.Robots;

namespace MkRss.Core.Crawling;

/// <summary>
/// 登録された巡回対象を順に処理する。
/// </summary>
public sealed class Crawler {
    /// <summary>robots.txt の設置場所。ホストの直下と定められている。</summary>
    private const string RobotsPath = "/robots.txt";

    private readonly ICrawlTargetSource _targets;
    private readonly IPageFetcher _fetcher;
    private readonly IArticleStore _store;
    private readonly CrawlerOptions _options;
    private readonly ILogger<Crawler> _logger;

    public Crawler(
        ICrawlTargetSource targets,
        IPageFetcher fetcher,
        IArticleStore store,
        CrawlerOptions options,
        ILogger<Crawler> logger) {
        _targets = targets;
        _fetcher = fetcher;
        _store = store;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 有効な巡回対象をすべて処理する。
    /// </summary>
    public async Task<CrawlReport> RunAsync(CancellationToken cancellationToken) {
        IReadOnlyList<CrawlTarget> targets = await _targets.ListEnabledAsync(cancellationToken);

        _logger.LogInformation("クロールを開始する。対象 {Count} 件。", targets.Count);

        List<SiteCrawlResult> results = [];
        Dictionary<string, RobotsTxt?> robotsByHost = [];

        foreach (CrawlTarget target in targets) {
            results.Add(await CrawlAsync(target, robotsByHost, cancellationToken));
        }

        return new CrawlReport(results);
    }

    private async Task<SiteCrawlResult> CrawlAsync(
        CrawlTarget target,
        Dictionary<string, RobotsTxt?> robotsByHost,
        CancellationToken cancellationToken) {
        if (!await IsAllowedAsync(target.PageUrl, robotsByHost, cancellationToken)) {
            _logger.LogInformation("{Site} は robots.txt により許可されていない。", target.Name);

            return new SiteCrawlResult(target.Id, CrawlOutcome.DisallowedByRobots, 0, 0, null);
        }

        PageFetchResult page = await _fetcher.FetchAsync(target.PageUrl, cancellationToken);

        if (!page.IsSuccess) {
            // 1サイトの失敗で全体を止めない。他サイトのフィード更新まで巻き添えにしないため。
            _logger.LogWarning(
                "{Site} の取得に失敗した。{Reason}", target.Name, page.FailureReason);

            return new SiteCrawlResult(
                target.Id, CrawlOutcome.FetchFailed, 0, 0, page.FailureReason);
        }

        IReadOnlyList<ExtractedArticle> articles;

        try {
            articles = HtmlArticleExtractor.Extract(
                page.Content,
                page.DeclaredCharset,
                target.PageUrl,
                target.Rule,
                page.FetchedAt);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            // セレクタの構文はDB登録時に検証していないため、壊れた規則が登録されうる。
            // 例外の型は抽出に使うライブラリの都合で決まるので、型を限定せずに受け止める。
            // キャンセルだけは失敗として扱わず、呼び出し側へ伝える。
            _logger.LogWarning(
                exception, "{Site} の抽出に失敗した。抽出規則を確認すること。", target.Name);

            return new SiteCrawlResult(
                target.Id, CrawlOutcome.ExtractionFailed, 0, 0, exception.Message);
        }

        int newArticleCount = await _store.SaveNewAsync(target.Id, articles, cancellationToken);

        _logger.LogInformation(
            "{Site} の処理が完了した。抽出 {Extracted} 件、新規 {Count} 件。",
            target.Name,
            articles.Count,
            newArticleCount);

        return new SiteCrawlResult(
            target.Id, CrawlOutcome.Succeeded, articles.Count, newArticleCount, null);
    }

    /// <summary>
    /// robots.txt に照らして <paramref name="pageUrl"/> の取得が許可されているかを判定する。
    /// </summary>
    /// <remarks>
    /// robots.txt を取得できない場合は許可として扱う。
    /// 設置していないサイトが多数あり、それらを巡回できなくなるほうが実害が大きいため。
    /// </remarks>
    private async Task<bool> IsAllowedAsync(
        Uri pageUrl,
        Dictionary<string, RobotsTxt?> robotsByHost,
        CancellationToken cancellationToken) {
        // 1回のクロール中は同じホストのrobots.txtを取得し直さない。
        // 同じサイトの複数の一覧ページを別々の巡回対象として登録できるため。
        if (!robotsByHost.TryGetValue(pageUrl.Authority, out RobotsTxt? robots)) {
            robots = await FetchRobotsAsync(pageUrl, cancellationToken);
            robotsByHost[pageUrl.Authority] = robots;
        }

        return robots is null || robots.IsAllowed(pageUrl.PathAndQuery, _options.UserAgent);
    }

    /// <summary>
    /// robots.txt を取得して解釈する。取得できない場合は <see langword="null"/> を返す。
    /// </summary>
    /// <remarks>
    /// 文字コードは UTF-8 として扱う。RFC 9309 がそう定めているため、
    /// HTMLのように申告やページ内の宣言を見て判定する必要はない。
    /// </remarks>
    private async Task<RobotsTxt?> FetchRobotsAsync(Uri pageUrl, CancellationToken cancellationToken) {
        PageFetchResult result = await _fetcher.FetchAsync(
            new Uri(pageUrl, RobotsPath), cancellationToken);

        return result.IsSuccess ? RobotsTxt.Parse(Encoding.UTF8.GetString(result.Content)) : null;
    }
}
