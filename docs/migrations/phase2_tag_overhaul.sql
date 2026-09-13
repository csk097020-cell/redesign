-- ─────────────────────────────────────────────────────────────────────────────
-- Phase 2 — Tag system overhaul migration
--
-- Run ONCE against the live Supabase project (SQL editor). Idempotent and safe to
-- re-run. Operates only on the system default tags (user_id is null); user-created
-- tags are never touched.
--
-- What it does:
--   1. Upserts the 10 new default category tags by name (recolors existing rows,
--      inserts missing ones). Overlapping names (Happy, Sad, Awe) reuse their
--      existing rows — no duplicates, IDs preserved — so any memory already tagged
--      with them keeps working.
--   2. Deactivates the non-overlapping legacy emotion defaults. They are NOT deleted:
--      their rows (and IDs) remain, so memories already tagged with them still
--      resolve and display (name-only). They simply stop being offered in pickers.
--
-- Nothing is ever deleted, so no applied tag is orphaned.
-- ─────────────────────────────────────────────────────────────────────────────

begin;

-- 1a. Recolor + reactivate any existing default row whose name is one of the new
--     categories (this is what preserves Happy / Sad / Awe without duplicating them).
update momo_tags t
set    color = v.color,
       is_active = true
from (values
    ('Adventure',    '#F97316'),
    ('Celebrations', '#EC4899'),
    ('Family',       '#22C55E'),
    ('Friends',      '#3B82F6'),
    ('Pets',         '#F59E0B'),
    ('Travel',       '#06B6D4'),
    ('Work',         '#6366F1'),
    ('Happy',        '#EAB308'),
    ('Sad',          '#8B5CF6'),
    ('Awe',          '#14B8A6')
) as v(name, color)
where t.user_id is null
  and t.name = v.name;

-- 1b. Insert the new categories that don't already exist as a default tag.
insert into momo_tags (name, color, is_active, user_id)
select v.name, v.color, true, null
from (values
    ('Adventure',    '#F97316'),
    ('Celebrations', '#EC4899'),
    ('Family',       '#22C55E'),
    ('Friends',      '#3B82F6'),
    ('Pets',         '#F59E0B'),
    ('Travel',       '#06B6D4'),
    ('Work',         '#6366F1'),
    ('Happy',        '#EAB308'),
    ('Sad',          '#8B5CF6'),
    ('Awe',          '#14B8A6')
) as v(name, color)
where not exists (
    select 1 from momo_tags t
    where t.user_id is null and t.name = v.name
);

-- 2. Deactivate the non-overlapping legacy emotion defaults (kept for display, hidden
--    from pickers). Happy / Sad / Awe are intentionally excluded — they are now
--    current categories handled above.
update momo_tags
set    is_active = false
where  user_id is null
  and  name in ('Grateful', 'Excited', 'Peaceful', 'Proud', 'Loved', 'Hopeful', 'Nostalgic');

commit;
