using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace MkRss.Infrastructure.Tests.Database;

/// <summary>
/// マイグレーション適用済みのPostgreSQLコンテナを1つ起動し、テスト間で共有する。
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime {
    /// <summary>
    /// 本番と同じイメージを使う。拡張機能や既定設定の差異で結果が変わらないようにするため、
    /// タグは固定し、更新は明示的に行う。
    /// </summary>
    private const string Image = "supabase/postgres:17.6.1.173";

    /// <summary>
    /// イメージ内蔵の初期化スクリプト(<c>migrate.sh</c>)が接続するロール名。
    /// </summary>
    /// <remarks>
    /// Testcontainers の既定値である <c>postgres</c> のままにすると、
    /// 初期化スクリプトが <c>role "supabase_admin" does not exist</c> で失敗し、
    /// サーバーが接続を受け付ける状態まで到達しない。
    /// </remarks>
    private const string SuperUser = "supabase_admin";

    /// <summary>
    /// イメージ既定のCMD(<c>postgres -D /etc/postgresql</c>)が指す設定ファイル。
    /// </summary>
    /// <remarks>
    /// Testcontainers はCMDを自身の起動オプションで置き換えるため、この指定が失われる。
    /// するとPostgreSQLは既定値で起動し、<c>listen_addresses</c> が <c>localhost</c> になる。
    /// コンテナ内の 127.0.0.1 しか待ち受けないため、ポートマッピング経由では接続できない。
    /// </remarks>
    private const string ConfigFile = "config_file=/etc/postgresql/postgresql.conf";

    /// <summary>
    /// マイグレーションSQLの複製先。csprojで出力ディレクトリへコピーしている。
    /// </summary>
    private const string MigrationsDirectory = "Migrations";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan ReadyPollingInterval = TimeSpan.FromSeconds(2);

    // Testcontainers はコンテナの生成時点でDocker接続を確認する。
    // 実行しない場合にDockerを要求しないよう、フィールド初期化子では生成しない。
    private PostgreSqlContainer? _container;

    private NpgsqlDataSource? _dataSource;

    /// <summary>
    /// 起動済みコンテナへの接続。
    /// </summary>
    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException(
            $"{nameof(InitializeAsync)} が完了していない。");

    public async ValueTask InitializeAsync() {
        // 実行しない場合はコンテナを起動しない。Dockerが無い環境で dotnet test を通すため。
        if (!IntegrationTestEnvironment.IsEnabled) {
            return;
        }

        _container = new PostgreSqlBuilder(Image)
            .WithUsername(SuperUser)
            .WithCommand("-c", ConfigFile)
            .Build();

        await _container.StartAsync();

        _dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());

        await WaitUntilConnectableAsync();
        await ApplyMigrationsAsync();
    }

    public async ValueTask DisposeAsync() {
        if (_dataSource is not null) {
            await _dataSource.DisposeAsync();
        }

        if (_container is not null) {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// 全テーブルを空にする。テストごとに呼び出して独立性を保つ。
    /// </summary>
    /// <remarks>
    /// スキーマを作り直さないのは、マイグレーションの再適用がテストごとの待ち時間になるため。
    /// </remarks>
    public async Task ResetAsync() {
        await ExecuteAsync("truncate table sites, articles, crawl_runs restart identity cascade;");
    }

    /// <summary>
    /// 実際に接続できるようになるまで待つ。
    /// </summary>
    /// <remarks>
    /// <c>supabase/postgres</c> は初期化の途中でサーバーを起動・停止する。
    /// Testcontainers の待機条件(<c>pg_isready</c>)を満たした直後でも、
    /// その停止に巻き込まれて接続が切られることがある。
    /// </remarks>
    private async Task WaitUntilConnectableAsync() {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + ReadyTimeout;

        while (true) {
            try {
                await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync();

                return;
            } catch (NpgsqlException) when (DateTimeOffset.UtcNow < deadline) {
                await Task.Delay(ReadyPollingInterval);
            }
        }
    }

    /// <summary>
    /// マイグレーションをファイル名順に適用する。順序がテーブル間の依存関係を満たす前提。
    /// </summary>
    private async Task ApplyMigrationsAsync() {
        IReadOnlyList<string> paths = EnumerateMigrations();

        if (paths.Count == 0) {
            throw new InvalidOperationException(
                $"マイグレーションが見つからない: {ResolveMigrationsDirectory()}");
        }

        foreach (string path in paths) {
            await ExecuteAsync(await File.ReadAllTextAsync(path));
        }
    }

    private static IReadOnlyList<string> EnumerateMigrations() {
        string directory = ResolveMigrationsDirectory();

        if (!Directory.Exists(directory)) {
            return [];
        }

        // Supabase CLI の命名規則(先頭がUTCタイムスタンプ)により、
        // 序数比較での昇順がそのまま適用順になる。
        return [.. Directory.EnumerateFiles(directory, "*.sql").OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static string ResolveMigrationsDirectory() {
        return Path.Combine(AppContext.BaseDirectory, MigrationsDirectory);
    }

    private async Task ExecuteAsync(string sql) {
        await using NpgsqlCommand command = DataSource.CreateCommand(sql);

        await command.ExecuteNonQueryAsync();
    }
}
