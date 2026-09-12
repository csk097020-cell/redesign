-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 003 — Profile avatar column + public 'avatars' storage bucket
-- Run once in the Supabase SQL editor. Idempotent.
--
-- Fixes two client-reported bugs that share one root cause (the avatar feature
-- was never provisioned in the backend):
--   • Bug #1 (name not persisting): GetProfileAsync SELECTs avatar_url, which did
--     not exist, so the whole profile read 400'd and the name read back null on
--     every reload. Adding the column makes the read succeed again.
--   • Bug #2 (avatar upload 404 "Bucket not found"): the app uploads to a public
--     'avatars' bucket that did not exist, and PATCHes momo_profiles.avatar_url.
--
-- Section 1 adds the column. Section 2 creates the bucket + RLS policies.
-- If your SQL-editor role lacks privileges to create policies on storage.objects,
-- use the dashboard fallback documented at the bottom of this file instead.
-- ─────────────────────────────────────────────────────────────────────────────


-- ── Section 1 — momo_profiles.avatar_url ─────────────────────────────────────
-- Stores the public Storage URL of the user's avatar. NULL until one is uploaded.

ALTER TABLE momo_profiles
    ADD COLUMN IF NOT EXISTS avatar_url text;


-- ── Section 2 — public 'avatars' storage bucket + policies ───────────────────
-- The app builds .../object/public/avatars/{user_id}/avatar.{ext} and binds it
-- straight into an Image, so the bucket must be PUBLIC (anon read). Writes are
-- folder-scoped to the owner, mirroring the user-videos policies. Upload uses
-- x-upsert:true, which performs an UPDATE when the object already exists, so an
-- UPDATE policy is required in addition to INSERT.

-- Create (or normalise to public) the bucket.
insert into storage.buckets (id, name, public)
values ('avatars', 'avatars', true)
on conflict (id) do update set public = true;

-- Policies are dropped-then-created so this script can be re-run safely.

-- Owner can upload into their own folder: avatars/{auth.uid()}/...
drop policy if exists "Users can upload their own avatar" on storage.objects;
create policy "Users can upload their own avatar"
    on storage.objects for insert
    to authenticated
    with check (
        bucket_id = 'avatars'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

-- Owner can overwrite their existing avatar (x-upsert performs an UPDATE).
drop policy if exists "Users can update their own avatar" on storage.objects;
create policy "Users can update their own avatar"
    on storage.objects for update
    to authenticated
    using (
        bucket_id = 'avatars'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

-- Public read so the avatar renders from its public URL (this is what actually
-- serves the image to the app). Remove this and flip the bucket to public=false
-- if you ever want avatars to be private/authenticated-only.
drop policy if exists "Public read access to avatars" on storage.objects;
create policy "Public read access to avatars"
    on storage.objects for select
    to public
    using ( bucket_id = 'avatars' );

-- Optional: allow owners to delete their avatar (not required — uploads upsert).
-- drop policy if exists "Users can delete their own avatar" on storage.objects;
-- create policy "Users can delete their own avatar"
--     on storage.objects for delete
--     to authenticated
--     using (
--         bucket_id = 'avatars'
--         and (storage.foldername(name))[1] = auth.uid()::text
--     );


-- ─────────────────────────────────────────────────────────────────────────────
-- Dashboard fallback (if creating storage.objects policies via SQL is blocked)
-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Storage → Buckets → "New bucket": name = avatars, Public bucket = ON, Save.
-- 2. Storage → Policies → on the 'avatars' bucket → "New policy":
--    a) INSERT, target role authenticated, definition:
--         bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text
--    b) UPDATE, target role authenticated, same definition as (a).
--    c) SELECT, target role public (anon), definition:
--         bucket_id = 'avatars'
-- 3. Section 1's ALTER must still be run in the SQL editor regardless.
