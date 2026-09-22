-- 巡回対象の一覧ページと、そこから記事情報を抽出するための規則。

create table sites (
  id          uuid        primary key default gen_random_uuid(),
  slug        text        not null unique,
  name        text        not null,
  page_url    text        not null,
  is_enabled  boolean     not null default true,

  -- 抽出規則 (MkRss.Core.Extraction.ExtractionRule に対応)。
  -- *_selector が空文字の場合は記事要素自身を対象とする。
  -- *_attribute が null の場合は要素のテキスト、値がある場合はその属性値を使う。
  item_selector    text     not null,
  title_selector   text     not null,
  title_attribute  text,
  link_selector    text     not null,
  link_attribute   text     not null default 'href',
  date_selector    text,
  date_attribute   text,

  -- タイムゾーンを持たない日付表記に当てるオフセット。
  date_offset      interval not null default '0',

  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),

  -- 公開URL /feeds/{slug}.xml のパスにそのまま置ける形式か。
  -- 初期値の生成規則 (ホスト名由来 + ランダム接尾辞) はDB側では保証しない。
  constraint sites_slug_format_check
    check (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),

  -- 空文字は「記事要素自身」を意味するが、item_selector ではその解釈が成り立たない。
  constraint sites_item_selector_not_blank
    check (length(btrim(item_selector)) > 0),

  -- 日付を抽出しない (date_selector is null) のに属性名だけ指定された行を防ぐ。
  constraint sites_date_pair_check
    check (date_selector is not null or date_attribute is null)
);
