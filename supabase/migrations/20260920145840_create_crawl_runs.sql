-- クロール実行の記録。
-- CRONは POST /api/crawl の 202 Accepted を受け取るだけで成否を知れないため、
-- exit code による失敗検知の代替として実行結果を残す。GET /api/status が最新行を返す。

create table crawl_runs (
  id                 uuid        primary key default gen_random_uuid(),
  started_at         timestamptz not null default now(),
  finished_at        timestamptz,
  status             text        not null,
  new_article_count  integer     not null default 0,
  error_message      text,

  -- Postgresのenum型は値の追加にマイグレーションが必要になるため、text + CHECK とする。
  constraint crawl_runs_status_check
    check (status in ('running', 'succeeded', 'failed'))
);

create index crawl_runs_started_at_idx on crawl_runs (started_at desc);
