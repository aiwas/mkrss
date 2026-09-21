using System;
using System.Collections.Generic;

namespace MkRss.Core.Robots;

/// <summary>
/// robots.txt の簡易判定。
/// </summary>
/// <remarks>
/// ワイルドカード(<c>*</c> / <c>$</c>)、<c>Crawl-delay</c>、<c>Sitemap</c> は解釈しない。
/// </remarks>
public sealed class RobotsTxt {
    private const string WildcardUserAgent = "*";

    /// <summary>1件の許可/拒否規則。</summary>
    /// <param name="Path">前方一致で比較するパス。</param>
    /// <param name="IsAllow"><c>Allow</c> なら <see langword="true"/>、<c>Disallow</c> なら <see langword="false"/>。</param>
    private sealed record Rule(string Path, bool IsAllow);

    /// <summary>User-agent ごとの規則。</summary>
    private readonly Dictionary<string, List<Rule>> _groups;

    private RobotsTxt(Dictionary<string, List<Rule>> groups) {
        _groups = groups;
    }

    /// <summary>
    /// robots.txt の内容を解釈する。
    /// </summary>
    public static RobotsTxt Parse(string content) {
        Dictionary<string, List<Rule>> groups = new(StringComparer.OrdinalIgnoreCase);

        // 直前の行が規則だった場合、次の User-agent 行は新しいグループの開始を意味する。
        List<string> currentAgents = [];
        bool startNewGroup = true;

        foreach (string rawLine in content.Split('\n')) {
            if (!TryReadDirective(rawLine, out string name, out string value)) {
                continue;
            }

            if (name.Equals("User-agent", StringComparison.OrdinalIgnoreCase)) {
                if (startNewGroup) {
                    currentAgents = [];
                    startNewGroup = false;
                }

                currentAgents.Add(value);

                continue;
            }

            startNewGroup = true;

            Rule? rule = ReadRule(name, value);

            if (rule is null) {
                continue;
            }

            foreach (string agent in currentAgents) {
                if (!groups.TryGetValue(agent, out List<Rule>? rules)) {
                    rules = [];
                    groups[agent] = rules;
                }

                rules.Add(rule);
            }
        }

        return new RobotsTxt(groups);
    }

    /// <summary>
    /// <paramref name="path"/> の取得が許可されているかを判定する。
    /// </summary>
    /// <param name="path">
    /// 対象URLのパス部分。RFC 9309 はクエリを含めて比較すると定めているため、
    /// 呼び出し側は <see cref="Uri.PathAndQuery"/> を渡す。
    /// </param>
    /// <param name="userAgent">判定に使う User-Agent。</param>
    public bool IsAllowed(string path, string userAgent) {
        // 自分宛てのグループがあればそれだけを見る。無ければ `*` のグループを見る。
        if (!_groups.TryGetValue(userAgent, out List<Rule>? rules)
            && !_groups.TryGetValue(WildcardUserAgent, out rules)) {
            return true;
        }

        // RFC 9309: 最も長く一致した規則が優先され、同じ長さなら Allow を優先する。
        Rule? best = null;

        foreach (Rule rule in rules) {
            if (!path.StartsWith(rule.Path, StringComparison.Ordinal)) {
                continue;
            }

            if (best is null
                || rule.Path.Length > best.Path.Length
                || (rule.Path.Length == best.Path.Length && rule.IsAllow)) {
                best = rule;
            }
        }

        return best is null || best.IsAllow;
    }

    /// <summary>
    /// 1行を <c>名前: 値</c> として読む。
    /// <c>#</c> 以降はコメントとして除去する。指令として解釈できない行は <see langword="false"/> を返す。
    /// </summary>
    private static bool TryReadDirective(string rawLine, out string name, out string value) {
        name = string.Empty;
        value = string.Empty;

        int commentIndex = rawLine.IndexOf('#', StringComparison.Ordinal);
        string line = commentIndex < 0 ? rawLine : rawLine[..commentIndex];

        int separatorIndex = line.IndexOf(':', StringComparison.Ordinal);

        if (separatorIndex < 0) {
            return false;
        }

        name = line[..separatorIndex].Trim();
        value = line[(separatorIndex + 1)..].Trim();

        return true;
    }

    /// <summary>
    /// <c>Allow</c> / <c>Disallow</c> の行を規則へ変換する。
    /// 規則として扱わない行は <see langword="null"/> を返す。
    /// 値が空の <c>Disallow</c> は「何も拒否しない」を意味するため規則にしない。
    /// </summary>
    private static Rule? ReadRule(string name, string value) {
        if (value.Length == 0) {
            return null;
        }

        if (name.Equals("Allow", StringComparison.OrdinalIgnoreCase)) {
            return new Rule(value, IsAllow: true);
        }

        if (name.Equals("Disallow", StringComparison.OrdinalIgnoreCase)) {
            return new Rule(value, IsAllow: false);
        }

        return null;
    }
}
