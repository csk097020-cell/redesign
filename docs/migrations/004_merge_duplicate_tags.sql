-- Migration 004 — Merge duplicate tags (case-insensitive) into one Title-Case canonical.
--
-- ⚠️  REVIEW BEFORE RUNNING. Run manually in the Supabase SQL editor. Do NOT auto-apply.
--     Take a snapshot / run on a Supabase branch or staging first.
--
-- Data model note: momo_memories.tags is a jsonb ARRAY of tag-id strings. There is NO
-- join table (no memory_tags). Repointing associations means rewriting those jsonb arrays.
--
-- Scope: merge only within the same owner. Per-user tags group by user_id; system/default
-- tags (user_id is null) group together separately. A user's tag is NEVER merged onto a
-- system tag, and two different users' identically-named tags are never merged with each other.
--
-- What it does:
--   1. Picks a canonical survivor per (owner, case-insensitive name): prefer active, then oldest.
--   2. Repoints every memory's jsonb tag array from duplicate ids to the survivor, de-duplicated.
--   3. Title-Cases the surviving tag names (initcap).
--   4. Deletes the orphaned duplicate tag rows.
--   5. Adds a unique index so future duplicates are blocked at the DB level.


-- ─────────────────────────────────────────────────────────────────────────────
-- Optional pre-run sanity check — how many duplicate groups exist, per owner.
-- Run this on its own FIRST to see what will be merged. It changes nothing.
-- ─────────────────────────────────────────────────────────────────────────────
-- select coalesce(user_id::text, 'system') as owner,
--        lower(btrim(name))                as norm_name,
--        count(*)                          as tag_count,
--        array_agg(name order by created_at) as names
-- from momo_tags
-- group by 1, 2
-- having count(*) > 1
-- order by tag_count desc, owner;


begin;

-- 1. Map each duplicate tag id -> its canonical survivor (prefer active, then oldest, then id).
create temporary table _dupe_map on commit drop as
with ranked as (
    select
        id,
        first_value(id) over (
            partition by coalesce(user_id::text, 'system'), lower(btrim(name))
            order by is_active desc, created_at asc, id asc
        ) as canonical_id
    from momo_tags
)
select id as dupe_id, canonical_id
from ranked
where id <> canonical_id;

-- 2. Repoint every memory's jsonb tag array from dupe ids to canonical, de-duplicated.
--    Only touch memories that actually reference at least one duplicate id.
update momo_memories m
set tags = coalesce((
        select jsonb_agg(distinct new_id)
        from (
            select coalesce(dm.canonical_id::text, elem) as new_id
            from jsonb_array_elements_text(m.tags) as elem
            left join _dupe_map dm on dm.dupe_id::text = elem
        ) mapped
    ), '[]'::jsonb)
where exists (
    select 1
    from jsonb_array_elements_text(m.tags) as elem
    join _dupe_map dm on dm.dupe_id::text = elem
);

-- 3. Title-Case the surviving canonical tag names (e.g. "sad" -> "Sad").
update momo_tags
set name = initcap(btrim(name))
where name <> initcap(btrim(name));

-- 4. Delete the now-orphaned duplicate tag rows.
delete from momo_tags
where id in (select dupe_id from _dupe_map);

-- 5. Block future duplicates: one name per owner, case-insensitive.
--    Safe: step 1 already collapsed each (owner, lower(btrim(name))) group to one row,
--    and initcap in step 3 does not change lower(btrim(name)), so no residual collision.
create unique index if not exists momo_tags_unique_name_per_owner
    on momo_tags (coalesce(user_id::text, 'system'), lower(btrim(name)));

commit;


-- ─────────────────────────────────────────────────────────────────────────────
-- Optional post-run verification — expect zero rows (no remaining duplicate groups).
-- ─────────────────────────────────────────────────────────────────────────────
-- select coalesce(user_id::text, 'system') as owner, lower(btrim(name)) as norm_name, count(*)
-- from momo_tags
-- group by 1, 2
-- having count(*) > 1;
