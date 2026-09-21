using Xunit;

namespace MkRss.Infrastructure.Tests.Database;

/// <summary>
/// DBを使うテストをまとめるコレクション。
/// </summary>
/// <remarks>
/// 全テストで1つのコンテナを共有し、並列実行を無効にする。
/// 並列に実行すると、あるテストの <see cref="PostgresFixture.ResetAsync"/> が
/// 別のテストのデータを消してしまう。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture> {
    public const string Name = "Database";
}
