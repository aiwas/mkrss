using System;

namespace MkRss.Infrastructure.Tests.Database;

/// <summary>
/// DBを使うテストを実行するかどうかの判定。
/// </summary>
/// <remarks>
/// Dockerを必要とするため、既定では実行しない。
/// xUnitのTraitによる除外 (<c>-trait- Category=Integration</c>) は
/// テストアセンブリを直接実行する場合にしか機能せず、<c>dotnet test</c> 経由では
/// 全件が除外されてしまうため、実行時の判定で切り替えている。
/// </remarks>
internal static class IntegrationTestEnvironment {
    /// <summary>
    /// 実行を有効にする環境変数の名前。
    /// </summary>
    public const string VariableName = "MKRSS_INTEGRATION_TESTS";

    /// <summary>
    /// スキップ時に表示する理由。
    /// </summary>
    public const string SkipReason =
        $"DBを使うテスト。実行するには {VariableName}=1 を設定する (Dockerが必要)。";

    public static bool IsEnabled {
        get {
            string? value = Environment.GetEnvironmentVariable(VariableName);

            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
