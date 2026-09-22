-- クロール実行における、サイト1件ごとの結果。
-- crawl_runs は全体の合計しか持たないため、これだけでは
-- 「そのサイトだけ記事を拾えていない」状態を検知できない。
-- 設計の根拠は docs/db-schema-draft.md を参照。

create table crawl_run_sites (
  id            uuid not null primary key default gen_random_uuid(),
  crawl_run_id  uuid not null references crawl_runs(id) on delete cascade,
  site_id       uuid not null references sites(id) on delete cascade,

  -- MkRss.Core.Crawling.CrawlOutcome に対応する。
  outcome       text not null,

  -- ページから抽出できた件数。既に保存済みのものも含む。
  -- 新規0件が「更新が無かった」のか「セレクタが機能していない」のかは、
  -- この列との差でしか区別できない (セレクタの構文をDB登録時に検証していないため)。
  extracted_article_count  integer not null default 0,
  new_article_count        integer not null default 0,

  failure_reason  text,

  -- Postgresのenum型は値の追加にマイグレーションが必要になるため、text + CHECK とする。
  -- crawl_runs.status と同じ方針。
  constraint crawl_run_sites_outcome_check
    check (outcome in ('succeeded', 'fetch_failed', 'extraction_failed', 'disallowed_by_robots')),

  -- 成功した、または robots.txt で除外された行に失敗理由が入っているのは矛盾している。
  -- 逆向き (失敗したのに理由が無い) は強制しない。理由を取得できない失敗を想定するため。
  constraint crawl_run_sites_failure_reason_check
    check (failure_reason is null or outcome in ('fetch_failed', 'extraction_failed')),

  -- 1回の実行で同じサイトを2度処理することはない。
  -- この一意制約は crawl_run_id 単独での検索にも使われるため、別のインデックスは設けない。
  constraint crawl_run_sites_run_site_unique unique (crawl_run_id, site_id)
);
