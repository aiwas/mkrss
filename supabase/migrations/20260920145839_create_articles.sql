-- 抽出済みの記事。フィードの中身になる。

create table articles (
  id              uuid        primary key default gen_random_uuid(),
  site_id         uuid        not null references sites(id) on delete cascade,

  -- MkRss.Core.Urls.UrlNormalizer を通した後のURL。生のURLは保持しない。
  normalized_url  text        not null,
  title           text        not null,

  -- 日付を抽出できなかった場合はページの取得日時が入る。
  published_at    timestamptz not null,
  first_seen_at   timestamptz not null default now(),

  -- 増分挿入の ON CONFLICT DO NOTHING が成立する前提。
  -- 同じ記事URLが複数サイトの一覧に載ることがあるため、サイト単位で一意とする。
  constraint articles_site_url_unique unique (site_id, normalized_url)
);

-- フィード配信時の「特定サイトの直近N件」を引くためのインデックス。
create index articles_site_published_at_idx on articles (site_id, published_at desc);
