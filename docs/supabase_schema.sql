-- Momentary Momentos — Supabase Schema
-- Run this in the Supabase SQL editor to set up the backend from scratch.
-- Tables use the momo_ prefix to avoid collisions with other projects on a shared instance.

-- ─────────────────────────────────────────────────────────────────────────────
-- Extensions
-- ─────────────────────────────────────────────────────────────────────────────

create extension if not exists "uuid-ossp";

-- ─────────────────────────────────────────────────────────────────────────────
-- momo_profiles
-- One row per auth user. Created automatically via trigger on auth.users.
-- ─────────────────────────────────────────────────────────────────────────────

-- ⚠️ THIS FILE HAS DRIFTED FROM THE LIVE DATABASE.
-- Profile saves use a PostgREST upsert (SupabaseService.UpdateFullNameAsync /
-- UpdateAvatarUrlAsync send POST with Prefer: resolution=merge-duplicates), which
-- requires INSERT privilege and an INSERT policy. Saving demonstrably works in
-- production, so the live database has at least one policy this file does not record.
-- Section 0 of docs/migrations/006_subscription_entitlement.sql dumps the real
-- policies and grants; paste the results back here. Do not write a migration that
-- revokes privileges based on this file alone.
--
-- avatar_url is added by migration 003; the premium_* columns by migration 006.

create table if not exists momo_profiles (
    id          uuid primary key references auth.users (id) on delete cascade,
    full_name   text,
    is_admin    boolean not null default false,
    is_premium  boolean not null default false,
    created_at  timestamptz not null default now(),

    -- Migration 003
    avatar_url  text,

    -- Migration 006: server-verified entitlement. is_premium is written only by the
    -- verify-subscription Edge Function via the service role; migration 006 revokes the
    -- authenticated role's UPDATE grant on it. premium_source = 'comp' marks a permanent
    -- grant with no store subscription behind it, which verification never revokes.
    premium_expires_at              timestamptz,
    premium_source                  text,
    premium_original_transaction_id text,
    premium_checked_at              timestamptz
);

alter table momo_profiles enable row level security;

create policy "Users can view their own profile"
    on momo_profiles for select
    using (auth.uid() = id);

-- Row filter only. RLS cannot restrict columns, so migration 006 additionally revokes
-- table-level UPDATE from `authenticated` and re-grants it on (full_name, avatar_url)
-- alone. Without those grants this policy would let any user set their own is_premium
-- and is_admin.
create policy "Users can update their own profile"
    on momo_profiles for update
    using (auth.uid() = id);

create policy "Admins can view all profiles"
    on momo_profiles for select
    using (
        exists (
            select 1 from momo_profiles
            where id = auth.uid() and is_admin = true
        )
    );

-- Trigger to auto-create a profile row when a new user signs up
create or replace function handle_new_user()
returns trigger language plpgsql security definer as $$
begin
    insert into momo_profiles (id, full_name)
    values (new.id, new.raw_user_meta_data->>'full_name');
    return new;
end;
$$;

create or replace trigger on_auth_user_created
    after insert on auth.users
    for each row execute procedure handle_new_user();

-- ─────────────────────────────────────────────────────────────────────────────
-- momo_tags
-- System tags (user_id is null) are shared across all users.
-- User-created tags have a user_id.
-- ─────────────────────────────────────────────────────────────────────────────

create table if not exists momo_tags (
    id          uuid primary key default uuid_generate_v4(),
    name        text not null,
    color       text not null default '#3B82F6',
    icon        text not null default '✨',
    is_active   boolean not null default true,
    user_id     uuid references auth.users (id) on delete cascade,
    created_at  timestamptz not null default now()
);

alter table momo_tags enable row level security;

create policy "Authenticated users can read active tags"
    on momo_tags for select
    to authenticated
    using (
        is_active = true
        and (user_id is null or user_id = auth.uid())
    );

create policy "Users can create their own tags"
    on momo_tags for insert
    to authenticated
    with check (user_id = auth.uid());

create policy "Users can update their own tags"
    on momo_tags for update
    to authenticated
    using (user_id = auth.uid());

create policy "Admins can manage all tags"
    on momo_tags for all
    using (
        exists (
            select 1 from momo_profiles
            where id = auth.uid() and is_admin = true
        )
    );

-- Seed default category tags (system-level, user_id = null).
-- Tags are name + color only; the icon column keeps its '✨' default and is no longer
-- shown in the app. Colors are spread across the curated app palette (see Utils/TagPalette.cs).
insert into momo_tags (name, color, is_active, user_id) values
    ('Adventure',    '#F97316', true, null),
    ('Celebrations', '#EC4899', true, null),
    ('Family',       '#22C55E', true, null),
    ('Friends',      '#3B82F6', true, null),
    ('Pets',         '#F59E0B', true, null),
    ('Travel',       '#06B6D4', true, null),
    ('Work',         '#6366F1', true, null),
    ('Happy',        '#EAB308', true, null),
    ('Sad',          '#8B5CF6', true, null),
    ('Awe',          '#14B8A6', true, null)
on conflict do nothing;

-- ─────────────────────────────────────────────────────────────────────────────
-- momo_memories
-- Core content table. tags column is a jsonb array of tag UUIDs.
-- video_url is null until the upload completes.
-- ─────────────────────────────────────────────────────────────────────────────

create table if not exists momo_memories (
    id          uuid primary key default uuid_generate_v4(),
    user_id     uuid not null references auth.users (id) on delete cascade,
    title       text not null,
    video_url   text,
    tags        jsonb not null default '[]',
    is_favorite boolean not null default false,
    created_at  timestamptz not null default now()
);

create index if not exists momo_memories_user_id_idx on momo_memories (user_id);
create index if not exists momo_memories_created_at_idx on momo_memories (created_at desc);

alter table momo_memories enable row level security;

create policy "Users can view their own memories"
    on momo_memories for select
    using (auth.uid() = user_id);

create policy "Users can insert their own memories"
    on momo_memories for insert
    with check (auth.uid() = user_id);

create policy "Users can update their own memories"
    on momo_memories for update
    using (auth.uid() = user_id);

create policy "Users can delete their own memories"
    on momo_memories for delete
    using (auth.uid() = user_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- momo_subscription_config
-- Single-row config table. Lets the client update pricing/limits without
-- shipping an app update (Contract Section 4.3).
-- ─────────────────────────────────────────────────────────────────────────────

create table if not exists momo_subscription_config (
    id                  integer primary key default 1,
    free_video_limit    integer not null default 100,
    paid_video_limit    integer not null default 500,
    monthly_price       numeric(10, 2) not null default 9.99,
    annual_price        numeric(10, 2) not null default 99.99,
    monthly_enabled     boolean not null default true,
    annual_enabled      boolean not null default true,
    constraint single_row check (id = 1)
);

alter table momo_subscription_config enable row level security;

create policy "Authenticated users can read subscription config"
    on momo_subscription_config for select
    to authenticated
    using (true);

create policy "Admins can update subscription config"
    on momo_subscription_config for all
    using (
        exists (
            select 1 from momo_profiles
            where id = auth.uid() and is_admin = true
        )
    );

-- Seed default config row
insert into momo_subscription_config (id, free_video_limit, paid_video_limit, monthly_price, annual_price)
values (1, 100, 500, 9.99, 99.99)
on conflict (id) do nothing;

-- ─────────────────────────────────────────────────────────────────────────────
-- Storage — user-videos and user-thumbnails buckets
-- Each user uploads to their own folder: {user_id}/{timestamp}.mp4
--
-- Both are public: SupabaseService returns /object/public/... URLs for playback and
-- previews, which 400 against a private bucket. The write policies below still scope
-- uploads to the caller's own folder.
-- ─────────────────────────────────────────────────────────────────────────────

insert into storage.buckets (id, name, public)
values ('user-videos', 'user-videos', true),
       ('user-thumbnails', 'user-thumbnails', true)
on conflict (id) do nothing;

create policy "Users can upload to their own folder"
    on storage.objects for insert
    to authenticated
    with check (
        bucket_id = 'user-videos'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

create policy "Users can read their own videos"
    on storage.objects for select
    to authenticated
    using (
        bucket_id = 'user-videos'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

create policy "Users can delete their own videos"
    on storage.objects for delete
    to authenticated
    using (
        bucket_id = 'user-videos'
        and (storage.foldername(name))[1] = auth.uid()::text
    );
