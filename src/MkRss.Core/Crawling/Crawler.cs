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

        _logger.LogInformation("クロール開始 (対象: {Count}件)", targets.Count);

        List<SiteCrawlResult> results = [];
        Dictionary<string, RobotsPolicy> robotsByOrigin = [];

        foreach (CrawlTarget target in targets) {
            results.Add(await CrawlAsync(target, robotsByOrigin, cancellationToken));
        }

        return new CrawlReport(results);
    }

    private async Task<SiteCrawlResult> CrawlAsync(
        CrawlTarget target,
        Dictionary<string, RobotsPolicy> robotsByOrigin,
        CancellationToken cancellationToken) {
        if (await CheckRobotsAsync(target, target.PageUrl, robotsByOrigin, cancellationToken)
            is SiteCrawlResult refused) {
            return refused;
        }

        PageFetchResult page = await _fetcher.FetchAsync(target.PageUrl, cancellationToken);

        if (!page.IsSuccess) {
            // 1サイトの失敗で全体を止めない。他サイトのフィード更新まで巻き添えにしないため。
            _logger.LogWarning(
                "取得失敗: {Site} ({Reason})", target.Name, page.FailureReason);

            return new SiteCrawlResult(
                target.Id, CrawlOutcome.FetchFailed, 0, 0, page.FailureReason);
        }

        // リダイレクト先は、取得する前には分からない。取得した後に、その場所の robots.txt で確かめる。
        // リダイレクト先への1回のリクエストは防げないが、拒否されていれば内容は使わない。
        if (page.FinalUrl != target.PageUrl
            && await CheckRobotsAsync(target, page.FinalUrl, robotsByOrigin, cancellationToken)
                is SiteCrawlResult refusedAfterRedirect) {
            return refusedAfterRedirect;
        }

        IReadOnlyList<ExtractedArticle> articles;

        try {
            // 相対URLは登録したURLではなく、リダイレクトを辿った後のURLを基準に解決する。
            // ブラウザと同じ基準にしないと、/news → /news/ のようなリダイレクトでリンク先がずれる。
            articles = HtmlArticleExtractor.Extract(
                page.Content,
                page.DeclaredCharset,
                page.FinalUrl,
                target.Rule,
                page.FetchedAt);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            // 管理UIは登録時にセレクタの構文を検証するが、SQLで直接登録した規則は検証を通らない。
            // 例外の型は抽出に使うライブラリの都合で決まるので、型を限定せずに受け止める。
            // キャンセルだけは失敗として扱わず、呼び出し側へ伝える。
            _logger.LogWarning(
                exception, "抽出失敗: {Site} (抽出規則を確認すること)", target.Name);

            return new SiteCrawlResult(
                target.Id, CrawlOutcome.ExtractionFailed, 0, 0, exception.Message);
        }

        int newArticleCount = await _store.SaveNewAsync(target.Id, articles, cancellationToken);

        // 0件は失敗として扱わないが、セレクタが機能していない可能性が高いため警告として残す。
        // 管理UIを開かなくても、ログから気づけるようにする。
        if (articles.Count == 0) {
            _logger.LogWarning("抽出0件: {Site} (抽出規則を確認すること)", target.Name);
        }

        _logger.LogInformation(
            "処理完了: {Site} (抽出: {Extracted}件、新規: {Count}件)",
            target.Name,
            articles.Count,
            newArticleCount);

        return new SiteCrawlResult(
            target.Id, CrawlOutcome.Succeeded, articles.Count, newArticleCount, null);
    }

    /// <summary>
    /// robots.txt に照らして <paramref name="url"/> を取得してよいかを確かめる。
    /// 取得してよい場合は <see langword="null"/>、そうでない場合はそのサイトの結果を返す。
    /// </summary>
    private async Task<SiteCrawlResult?> CheckRobotsAsync(
        CrawlTarget target,
        Uri url,
        Dictionary<string, RobotsPolicy> robotsByOrigin,
        CancellationToken cancellationToken) {
        RobotsPolicy robots = await GetRobotsPolicyAsync(url, robotsByOrigin, cancellationToken);

        if (robots.UnreachableReason is not null) {
            string reason = $"robots.txt を取得できない ({robots.UnreachableReason})";
            _logger.LogWarning("取得失敗: {Site} ({Reason})", target.Name, reason);

            return new SiteCrawlResult(target.Id, CrawlOutcome.FetchFailed, 0, 0, reason);
        }

        if (robots.Rules?.IsAllowed(url.PathAndQuery, _options.ProductToken) == false) {
            _logger.LogInformation("robots.txt により除外: {Site} ({Url})", target.Name, url);

            return new SiteCrawlResult(target.Id, CrawlOutcome.DisallowedByRobots, 0, 0, null);
        }

        return null;
    }

    /// <summary>
    /// <paramref name="pageUrl"/> の origin に適用する robots.txt を返す。
    /// </summary>
    private async Task<RobotsPolicy> GetRobotsPolicyAsync(
        Uri pageUrl,
        Dictionary<string, RobotsPolicy> robotsByOrigin,
        CancellationToken cancellationToken) {
        // 1回のクロール中は同じ origin のrobots.txtを取得し直さない。
        // 同じサイトの複数の一覧ページを別々の巡回対象として登録できるため。
        // RFC 9309 は robots.txt の適用範囲を origin(スキーム + ホスト + ポート)と定めている。
        // Uri.Authority はスキームを含まないため、キーには使えない。
        string origin = pageUrl.GetLeftPart(UriPartial.Authority);

        if (!robotsByOrigin.TryGetValue(origin, out RobotsPolicy? robots)) {
            robots = await FetchRobotsAsync(pageUrl, cancellationToken);
            robotsByOrigin[origin] = robots;
        }

        return robots;
    }

    /// <summary>
    /// robots.txt を取得して解釈する。
    /// </summary>
    /// <remarks>
    /// 文字コードは UTF-8 として扱う。RFC 9309 がそう定めているため、
    /// HTMLのように申告やページ内の宣言を見て判定する必要はない。
    /// </remarks>
    private async Task<RobotsPolicy> FetchRobotsAsync(Uri pageUrl, CancellationToken cancellationToken) {
        PageFetchResult result = await _fetcher.FetchAsync(
            new Uri(pageUrl, RobotsPath), cancellationToken);

        if (result.IsSuccess) {
            return new RobotsPolicy(RobotsTxt.Parse(Encoding.UTF8.GetString(result.Content)), null);
        }

        // RFC 9309 : 5xx と、応答を受け取れない場合(ネットワークエラー、タイムアウト)は全面的な拒否とみなす。
        // 429 は 4xx だが同じ扱いにする。相手が負荷を嫌っているときに巡回を続けないため(Google と同じ扱い)。
        if (result.FailureStatusCode is null or 429 or >= 500) {
            return new RobotsPolicy(null, result.FailureReason);
        }

        // それ以外の 4xx は robots.txt が無いものとして、制限なしで巡回する(RFC 9309)。
        // 設置していないサイトが多数あるため。
        return new RobotsPolicy(null, null);
    }

    /// <summary>
    /// origin ごとの robots.txt の取得結果。
    /// </summary>
    /// <param name="Rules">解釈した内容。設置されていない場合は <see langword="null"/> で、制限なしを表す。</param>
    /// <param name="UnreachableReason">
    /// 取得できなかった理由。取得できなかった場合は巡回しない。それ以外は <see langword="null"/>。
    /// </param>
    private sealed record RobotsPolicy(RobotsTxt? Rules, string? UnreachableReason);
}
